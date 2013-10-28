using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.Util;

namespace mmcv
{
    public partial class Form1 : Form
    {
        //declaring global variables
        private Capture capture;        //takes images from camera as image frames
        private bool captureInProgress;

        //FPS support
        private long tickCouner;
        private int frameCounter = 0;
        private int FPS = 0;
        //end.

        public Form1()
        {
            InitializeComponent();

        }
        //------------------------------------------------------------------------------//
        //Process Frame() below is our user defined function in which we will create an EmguCv 
        //type image called ImageFrame. capture a frame from camera and allocate it to our 
        //ImageFrame. then show this image in ourEmguCV imageBox
        //------------------------------------------------------------------------------//
        private void ProcessFrame(object sender, EventArgs arg)
        {
            Image<Bgr, Byte> imageFrame=null;
            while (imageFrame == null)
            {
                imageFrame = capture.QueryFrame();
            }

            imageFrame = imageFrame.Flip(Emgu.CV.CvEnum.FLIP.HORIZONTAL);

            Image<Gray, byte> grayFrame = imageFrame
               .Convert<Gray, byte>()                               //to gray image
               .ThresholdBinary(new Gray(127), new Gray(255))       //first treshold to get only part of interesting pixels
               .SmoothGaussian(11)                                  //gaussian filter to make smooth black & white pixels on edges
               .ThresholdBinary(new Gray(127), new Gray(255))       //once again treshold to make nice edges (thanks to gauss)
               .Dilate(3);                                          //enlarge dots

            Image<Gray, byte> finalFrame = new Image<Gray, byte>(grayFrame.Width, grayFrame.Height, new Gray(255));

            finalFrame.Draw(
                grayFrame
                .FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_LINK_RUNS, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE),
                    new Gray(0), new Gray(255), 1, 2, new Point(0, 0)
            );

            imageFrame.Draw(
                grayFrame
                .FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_LINK_RUNS, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE),
                    new Bgr(Color.White), new Bgr(Color.Black), 1, 2, new Point(0, 0)
            );

            addFPS(imageFrame, 10, 30);

            if (finalFrame != null)
            {
                imageBox2.Image = finalFrame;
                imageBox1.Image = imageFrame;
            }
        }

        private void addFPS(Image<Bgr, Byte> imageFrame, int x, int y)
        {
            frameCounter++;
            long seconds = DateTime.Now.Ticks / TimeSpan.TicksPerSecond;
            if (tickCouner != seconds)
            {
                FPS = frameCounter;
                tickCouner = seconds;
                frameCounter = 0;
            }

            MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_TRIPLEX, 1, 1);
            imageFrame.Draw("FPS: " + FPS, ref font, new Point(x, y), new Bgr(Color.Green));
        }

        private void ReleaseData()
        {
            if (capture != null)
                capture.Dispose();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            #region if capture is not created, create it now
            if (capture == null)
            {
                try
                {
                    capture = new Capture();
                }
                catch (NullReferenceException excpt)
                {
                    MessageBox.Show(excpt.Message);
                }
            }
            #endregion

            if (capture != null)
            {
                if (captureInProgress)
                {  //if camera is getting frames then stop the capture and set button Text
                    // "Start" for resuming capture
                    button1.Text = "Start!"; //
                    Application.Idle -= ProcessFrame;
                }
                else
                {
                    //if camera is NOT getting frames then start the capture and set button
                    // Text to "Stop" for pausing capture
                    button1.Text = "Stop";
                    Application.Idle += ProcessFrame;
                }

                captureInProgress = !captureInProgress;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}

