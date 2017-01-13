using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FoundationMM
{
    class ListViewItemNumberComparer : IComparer
    {
        private int col;
        private SortOrder order;
        public ListViewItemNumberComparer()
        {
            col = 0;
            order = SortOrder.Ascending;
        }
        public ListViewItemNumberComparer(int column, SortOrder order)
        {
            col = column;
            this.order = order;
        }
        public int Compare(object x, object y)
        {
            int returnVal = -1;

            //Splits the string at '/' into string array (for fraction-sort parsing).
            // [0] = numerator; [1] = denominator.
            // non-fraction strings only get one element, so [1] is not used.
            string[] fractA = ((ListViewItem)x).SubItems[col].Text.Split('/');
            string[] fractB = ((ListViewItem)y).SubItems[col].Text.Split('/');

            // These will be used in the actual "String.Compare()" method.
            string stringA = string.Empty;
            string stringB = string.Empty;

            // Initializing these to 1 so that non-fraction strings get divided by 1 instead
            // of zero.
            decimal numeratorA = 1, denominatorA = 1, numeratorB = 1, denominatorB = 1;

            // Parse string numerators and denominators to int, parse to decimal & 
            // divide them, and finally parse back into string...
            decimal.TryParse(fractA[0], out numeratorA);
            decimal.TryParse(fractB[0], out numeratorB);
            // non-fraction strings only get one element, so [1] is not used.
            if (stringA.Count() == 2 && stringB.Count() == 2)
            {
                decimal.TryParse(fractA[1], out denominatorA);
                decimal.TryParse(fractB[1], out denominatorB);
            }

            decimal decA = numeratorA / denominatorA;
            decimal decB = numeratorB / denominatorB;

            // This is so that the ping column gets reversed.
            if (fractA.Count() == 1 && fractB.Count() == 1)
            {
                decimal temp = decA;
                decA = decB;
                decB = temp;
            }

            if (decA > decB)
            {
                stringA = "0";
                stringB = "1";
            }
            else
            {
                stringA = "1";
                stringB = "0";
            }

            returnVal = String.Compare(stringA, stringB);
            if (order == SortOrder.Descending) { returnVal *= -1; }
            return returnVal;
        }
    }
}
