
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using mmcv;

namespace exportLib
{
    public class HandCTRLibrary
    {
        private Form1 myForm = null;

        public HandCTRLibrary() {
            myForm = new mmcv.Form1();
            myForm.Show();
        }

        public int getNumberOfFingers() {

            Hand hand = myForm.getHand();
            if (hand != null)
                return hand.FingersCount;
            return 0;
        }
    }
}
