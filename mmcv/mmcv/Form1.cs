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

        //switch to HSV calibrate
        private bool calibrationHSVLevel = false;
        private double hue_min = 0, hue_max = 14;
        private double saturation_min = 92, saturation_max = 172;
        private double value_min = 96, value_max = 255;
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
            Image<Hsv, Byte> hsvFrame = imageFrame.Convert<Hsv, Byte>();

            if (calibrationHSVLevel)
            {
                CalibrateHSV(ref hsvFrame);
            }
            else
            {

                /*/--just play with contour display--
                Image<Gray, byte> grayFrame = imageFrame
                   .Convert<Gray, byte>()                               //to gray image
                   .ThresholdBinary(new Gray(127), new Gray(255))       //first treshold to get only part of interesting pixels
                   .SmoothGaussian(11)                                  //gaussian filter to make smooth black & white pixels on edges
                   .ThresholdBinary(new Gray(127), new Gray(255))       //once again treshold to make nice edges (thanks to gauss)
                   .Dilate(3);                                          //enlarge dots

                Image<Gray, byte> finalFrame = new Image<Gray, byte>(grayFrame.Width, grayFrame.Height, new Gray(255));

                Contour<Point> contours = grayFrame
                    .FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_LINK_RUNS, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE);

                if (contours != null)
                {
                    finalFrame.Draw(
                        contours, new Gray(0), new Gray(255), 1, 2, new Point(0, 0)
                    );

                    imageFrame.Draw(
                        contours, new Bgr(Color.White), new Bgr(Color.Black), 1, 2, new Point(0, 0)
                    );
                }
                //--end--

                addFPS(imageFrame, 10, 30);

                imageBox2.Image = finalFrame;
                imageBox1.Image = imageFrame;*/ 
            }

            addFPS(imageFrame, 10, 30);

            imageBox1.Image = imageFrame;
            imageBox2.Image = hsvFrame;
        }

        private void CalibrateHSV(ref Image<Hsv, Byte> hsvImage)
        {
            float horizontalFactor = 0.2f;
            float verticalFactor = 0.2f;

            int rectWidth = (int)(hsvImage.Width * horizontalFactor);
            int rectHeight = (int)(hsvImage.Height * verticalFactor);

            int topLeftX = (int)((((float)hsvImage.Width / 2) - rectWidth) / 2);
            int topLeftY = (int)(((float)hsvImage.Height - rectHeight) / 2);

            Rectangle rangeOfInterest = new Rectangle(topLeftX, topLeftY, rectWidth, rectHeight);

            //CircleF circle = new CircleF(new PointF(hsvImage.Width / 5, hsvImage.Height / 2), 50);

            hsvImage.Draw(rangeOfInterest, new Hsv(255,255,255), 3);

            Image<Gray, Byte> maskedImage = hsvImage.InRange(
                new Hsv(hue_min, saturation_min, value_min),
                new Hsv(hue_max, saturation_max, value_max));

            hsvImage = maskedImage.Convert<Hsv, Byte>() //tu powstaje jakiś "First chance of exception..."
                .SmoothGaussian(5)
                .Dilate(1)
                .Convert<Rgb, Byte>()
                .ThresholdBinary(new Rgb(127,127,127), new Rgb(255,255,255))
                .Convert<Hsv, Byte>();

            hsvImage.Draw(rangeOfInterest, new Hsv(255, 255, 255), 3);

            //hsvImage.ROI = rangeOfInterest;

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
            button1.PerformClick();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            calibrationHSVLevel = !calibrationHSVLevel;
            panelHsvSettings.Enabled = calibrationHSVLevel;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            hue_min = Convert.ToDouble((sender as NumericUpDown).Value);
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            hue_max = Convert.ToDouble((sender as NumericUpDown).Value);
        }

        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            saturation_min = Convert.ToDouble((sender as NumericUpDown).Value);
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            saturation_max = Convert.ToDouble((sender as NumericUpDown).Value);
        }

        private void numericUpDown6_ValueChanged(object sender, EventArgs e)
        {
            value_min = Convert.ToDouble((sender as NumericUpDown).Value);
        }

        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            value_max = Convert.ToDouble((sender as NumericUpDown).Value);
        }
    }
}

