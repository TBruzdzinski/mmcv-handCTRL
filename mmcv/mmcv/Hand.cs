using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mmcv
{
    class Hand
    {
        private int fingersCount;
        private int height;
        private int screenHeight;
        private DateTime lastChange;
        private int left;
        private int top;

        public int FingersCount
        {
            get { return fingersCount; }
        }
        public int Height
        {
            get { return height; }
        }
        public int ScreenHeight
        {
            get { return screenHeight; }
        }
        public DateTime LastChange
        {
            get { return lastChange; }
        }
        public int Left
        {
            get { return left; }
        }
        public int Top
        {
            get { return top; }
        }

        public Hand(int fingersCount, int height, int screenHeight, int left, int top)
        {
            this.fingersCount = fingersCount;
            this.height = height;
            this.screenHeight = screenHeight;
            this.left = left;
            this.top = top;
            lastChange = new DateTime();
        }

        public float HandSizeRatio()
        {
            return (float)height / screenHeight;
        }
    }
}
