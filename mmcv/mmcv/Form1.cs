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
            Image<Bgr, Byte> ImageFrame = capture.QueryFrame();

            addFPS(ImageFrame, 10, 20);

            imageBox1.Image = ImageFrame;
        }

        private void addFPS(Image<Bgr, Byte> ImageFrame, int x, int y)
        {
            frameCounter++;
            long seconds = DateTime.Now.Ticks / TimeSpan.TicksPerSecond;
            if (tickCouner != seconds)
            {
                FPS = frameCounter;
                tickCouner = seconds;
                frameCounter = 0;
            }
            MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_PLAIN, 1, 1);
            ImageFrame.Draw("FPS: " + FPS, ref font, new Point(x, y), new Bgr(Color.Black));
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
    }
}

