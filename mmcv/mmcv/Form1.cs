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
        private double hue_min = 0, hue_max = 18;
        private double saturation_min = 0, saturation_max = 153;
        private double value_min = 82, value_max = 255;
        private DenseHistogram histogram = null;
        //u krzyska (noc): 0,18|0,153|82,255
        //u mnie (noc): 0,16|8,111|76,255
        //end.

        //Hand recognition
        private Seq<Point> hull;
        private Seq<Point> filteredHull;
        private Seq<MCvConvexityDefect> defects;
        private MCvConvexityDefect[] defectsArr;
        private MCvBox2D box;
        private Rectangle handRect;
        private Ellipse ellip;
        //end.

        private Hand hand = null;

        private int[] fingerCountArray = new int[5];

        public Form1()
        {
            InitializeComponent();

            for (int i = 0; i < fingerCountArray.Length; i++)
                fingerCountArray[i] = 0;
        }


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
                CalibrateHSV(ref hsvFrame, ref histogram);
                imageFrame = hsvFrame.Convert<Bgr, byte>();
            }
            else
            {

                if (histogram != null)
                {

                     //* //BACKPROJECTION
                     //* 
                     //* powinno działać, ale nie chce - nie wiem czemu, a nie ma nic gorszego od czytania histogramów
                     //* 
                    /*Image<Gray, byte>[] channels = imageFrame.Split();
                    Image<Gray, byte> backProjection = histogram.BackProject<byte>(channels);

                    Image<Gray, byte> mask = hsvFrame.InRange(
                        new Hsv(hue_min, saturation_min, value_min),
                        new Hsv(hue_max, saturation_max, value_max)
                    );
                    backProjection.And(mask);

                    hsvFrame = backProjection.Convert<Hsv, byte>().Dilate(5).Erode(3);//*/
                    
                    Hsv Hsv_min = new Hsv(hue_min, saturation_min, value_min);
                    Hsv Hsv_max = new Hsv(hue_max, saturation_max, value_max);

                    Image<Gray, Byte> skin = getSkinOnImage(hsvFrame, Hsv_min, Hsv_max);
                    FindContourAndConvexHull(skin, imageFrame);
                    hand = ComputeHandInfo(imageFrame);

                    if (hand != null)
                    {
                        label16.Text = "" + hand.FingersCount;
                        label15.Text = "" + hand.HandSizeRatio();
                        label14.Text = "" + hand.Height;
                        label13.Text = "" + hand.Left;
                        label12.Text = "" + hand.Top;
                    }
                }
                
            }

            addFPS(imageFrame, 10, 30);

            imageBox1.Image = imageFrame;
            //imageBox2.Image = hsvFrame;
        }

        private Image<Gray, byte> getSkinOnImage(Image<Hsv, byte> sourceImage, Hsv Hsv_min, Hsv Hsv_Max)
        {
            Image<Gray, Byte> skin = sourceImage.InRange(Hsv_min, Hsv_Max);

            skin = skin
                .SmoothGaussian(11)
                .Dilate(3)
                .SmoothGaussian(5)
                .Convert<Rgb, Byte>()
                .ThresholdBinary(new Rgb(127, 127, 127), new Rgb(255, 255, 255))
                .Convert<Gray, Byte>();

            return skin;
        }

        private void FindContourAndConvexHull(Image<Gray, byte> skin, Image<Bgr, byte> imageFrame)
        {
            using (MemStorage cacheStorage = new MemStorage())
            {
                //getting the countours by simplest algorithm and list in retourn (tree isn't necessary)
                Contour<Point> contours = skin.FindContours(
                    Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                    Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST,
                    cacheStorage
                );

                //this variable will contain the biggest countour (if any avaiable)
                Contour<Point> largestContour = null;

                //searching for biggest countour
                Double CurrArea = 0, MaxArea = 0;
                while (contours != null)
                {
                    CurrArea = contours.Area;
                    if (CurrArea > MaxArea)
                    {
                        MaxArea = CurrArea;
                        largestContour = contours;
                    }
                    contours = contours.HNext;
                }

                if (largestContour != null)
                {
                    //drawing oryginal countour on image:
                    imageFrame.Draw(largestContour, new Bgr(Color.DarkViolet), 2);

                    //smoothing a bit the countour to make less amout of defects + draw:
                    Contour<Point> currentContour = largestContour.ApproxPoly(largestContour.Perimeter * 0.0025, cacheStorage);
                    imageFrame.Draw(currentContour, new Bgr(Color.LimeGreen), 2);
                    largestContour = currentContour;

                    //computing and drawing convex hull (smallest polygon that covers whole hand):
                    hull = largestContour.GetConvexHull(Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    imageFrame.DrawPolyline(hull.ToArray(), true, new Bgr(200, 125, 75), 2);

                    //computing smallest box (with angle), that covers the hull, and drawing without angle:
                    box = largestContour.GetMinAreaRect();
                    handRect = box.MinAreaRect();
                    imageFrame.Draw(handRect, new Bgr(200, 0, 0), 1);

                    //drawing the center of the box iwth hull:
                    imageFrame.Draw(new CircleF(new PointF(box.center.X, box.center.Y), 3), new Bgr(200, 125, 75), 2);

                    //drawing ellipse ("E") that containts most of the foun pixels:
                    if (largestContour.Count() >= 5)
                    {
                        ellip.MCvBox2D = CvInvoke.cvFitEllipse2(largestContour.Ptr);
                        imageFrame.Draw(new Ellipse(ellip.MCvBox2D), new Bgr(Color.LavenderBlush), 3);
                    }

                    //computing and drawing minimal enclosing circle, that contains whole contour:
                    PointF center;
                    float radius;
                    CvInvoke.cvMinEnclosingCircle(largestContour.Ptr, out  center, out  radius);
                    imageFrame.Draw(new CircleF(center, radius), new Bgr(Color.Gold), 2);

                    //drawing center of ellipse "E":
                    imageFrame.Draw(new CircleF(new PointF(ellip.MCvBox2D.center.X, ellip.MCvBox2D.center.Y), 3), new Bgr(100, 25, 55), 2);
                    imageFrame.Draw(ellip, new Bgr(Color.DeepPink), 2);

                    //computing and drawing ellipse ("F") that shows the direction of hand:
                    CvInvoke.cvEllipse(imageFrame,
                        new Point((int)ellip.MCvBox2D.center.X, (int)ellip.MCvBox2D.center.Y),
                        new Size((int)ellip.MCvBox2D.size.Width, (int)ellip.MCvBox2D.size.Height),
                        ellip.MCvBox2D.angle,
                        0,
                        360,
                        new MCvScalar(120, 233, 88),
                        1,
                        Emgu.CV.CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                        0);

                    //drawing ellipse, that's small, but also shows the direction of hand:
                    imageFrame.Draw(
                        new Ellipse(
                            new PointF(box.center.X, box.center.Y),
                            new SizeF(box.size.Height, box.size.Width),
                            box.angle),
                        new Bgr(0, 0, 0), 2);


                    //algorithm that fiters convex hull. It saves only those points, that have distance
                    //between next point bigger than 1/10th of the box size. Small ones are removed.
                    filteredHull = new Seq<Point>(cacheStorage);
                    for (int i = 0; i < hull.Total; i++)
                    {
                        if (Math.Sqrt(Math.Pow(hull[i].X - hull[i + 1].X, 2) + Math.Pow(hull[i].Y - hull[i + 1].Y, 2)) > box.size.Width / 10)
                        {
                            filteredHull.Push(hull[i]);
                        }
                    }

                    //finding convex hull defects:
                    defects = largestContour.GetConvexityDefacts(cacheStorage, Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    defectsArr = defects.ToArray();
                }
            }
        }

        private Hand ComputeHandInfo(Image<Bgr, byte> imageFrame)
        {
            int fingerNumber = 0;

            #region defects drawing
            if (defects == null)
            {
                return null;
            }

            //iterating all found defects
            for (int i = 0; i < defects.Total; i++)
            {
                PointF beginPoint, depthPoint, endPoint;
                try
                {
                    //defect has three points: begin, depth and end. They're computed here:
                    beginPoint = new PointF((float)defectsArr[i].StartPoint.X,
                                                    (float)defectsArr[i].StartPoint.Y);

                    depthPoint = new PointF((float)defectsArr[i].DepthPoint.X,
                                                    (float)defectsArr[i].DepthPoint.Y);

                    endPoint = new PointF((float)defectsArr[i].EndPoint.X,
                                                    (float)defectsArr[i].EndPoint.Y);
                }
                catch (Exception e)
                {
                    return null;
                }

                //nice looking lines connecting defects begin and end, with it's depth point:
                LineSegment2D startDepthLine = new LineSegment2D(defectsArr[i].StartPoint, defectsArr[i].DepthPoint);
                LineSegment2D depthEndLine = new LineSegment2D(defectsArr[i].DepthPoint, defectsArr[i].EndPoint);

                //circles at the begin,depth,end points (for computing purposes):
                CircleF beginCircle = new CircleF(beginPoint, 5f);
                CircleF depthCircle = new CircleF(depthPoint, 5f);
                CircleF endCircle = new CircleF(endPoint, 5f);

                //heuristic that decides if the defect in convect hull can caused by finger:
                if ((beginCircle.Center.Y < box.center.Y || depthCircle.Center.Y < box.center.Y) && (beginCircle.Center.Y < depthCircle.Center.Y) && (Math.Sqrt(Math.Pow(beginCircle.Center.X - depthCircle.Center.X, 2) + Math.Pow(beginCircle.Center.Y - depthCircle.Center.Y, 2)) > box.size.Height / 6.5))
                {
                    fingerNumber++;
                    imageFrame.Draw(startDepthLine, new Bgr(Color.Green), 2);
                    imageFrame.Draw(depthEndLine, new Bgr(Color.Magenta), 2);
                }

                //finally also we can draw the dots:
                imageFrame.Draw(beginCircle, new Bgr(Color.Red), 2);
                imageFrame.Draw(depthCircle, new Bgr(Color.Yellow), 5);
                imageFrame.Draw(endCircle, new Bgr(Color.DarkBlue), 4);
            }
            #endregion

            //Console.Write(fingerNumber + " | ");

            for (int j = 1; j < fingerCountArray.Length; j++)
            {
                fingerCountArray[j - 1] = fingerCountArray[j];
            }
            fingerCountArray[fingerCountArray.Length - 1] = fingerNumber;

            int[] tmpArr = new int[fingerCountArray.Length];
            for (int k = 0; k < tmpArr.Length; k++)
            {
                tmpArr[k] = fingerCountArray[k];
            }
            Array.Sort(tmpArr);

            fingerNumber = tmpArr[tmpArr.Length / 2];

            Console.WriteLine(string.Join(",", fingerCountArray));

            //drawing the number of visible fingers:
            MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 5d, 5d);
            imageFrame.Draw(fingerNumber.ToString(), ref font, new Point(50, 150), new Bgr(Color.White));

            return new Hand(fingerNumber, handRect.Height, imageFrame.Size.Height, handRect.Left, handRect.Top);
        }

        private void CalibrateHSV(ref Image<Hsv, Byte> hsvImage, ref DenseHistogram histogram)
        {
            float horizontalFactor = 0.2f;
            float verticalFactor = 0.2f;

            int rectWidth = (int)(hsvImage.Width * horizontalFactor);
            int rectHeight = (int)(hsvImage.Height * verticalFactor);

            int topLeftX = (int)((((float)hsvImage.Width / 2) - rectWidth) / 2);
            int topLeftY = (int)(((float)hsvImage.Height - rectHeight) / 2);

            Rectangle rangeOfInterest = new Rectangle(topLeftX, topLeftY, rectWidth, rectHeight);

            Image<Gray, Byte> maskedImage = hsvImage.InRange(
                new Hsv(hue_min, saturation_min, value_min),
                new Hsv(hue_max, saturation_max, value_max));

            Image<Hsv, byte> partToCompute = hsvImage.Copy(rangeOfInterest);

            int[] h_bins = { 30, 30 };
            RangeF[] h_ranges = {
                new RangeF(0, 180),
                new RangeF(0, 255)
            };
            Image<Gray, byte>[] channels = partToCompute.Split().Take(2).ToArray();

            histogram = new DenseHistogram(h_bins, h_ranges);
            histogram.Calculate(channels, true, null);

            float minValue, maxValue;
            int[] posMinValue, posMaxValue;
            histogram.MinMax(out minValue, out maxValue, out posMinValue, out posMaxValue);
            histogram.Threshold(
                (double)minValue + (maxValue - minValue) * 40 / 100
            );

            hsvImage = maskedImage.Convert<Hsv, Byte>() //tu powstaje jakiś "First chance of exception..."
                .SmoothGaussian(5)
                .Dilate(1)
                .Convert<Rgb, Byte>()
                .ThresholdBinary(new Rgb(127,127,127), new Rgb(255,255,255))
                .Convert<Hsv, Byte>();

            //hsvImage.Draw(rangeOfInterest, new Hsv(255, 255, 255), 3);
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

        private void button5_Click(object sender, EventArgs e)
        {

        }

        public Hand getHand()
        {
            return hand;
        }

        public int GetFingerNumber()
        {
            if (hand != null)
                return hand.FingersCount;
            return 0;
        }

        public int FingerCount
        {
            get
            {
                if (hand != null)
                    return hand.FingersCount;
                return 0;
            }
        }
    }
}

