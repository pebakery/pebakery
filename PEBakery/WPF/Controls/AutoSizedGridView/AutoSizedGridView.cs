/*
    Derived from http://stackoverflow.com/a/15745082

    MIT License (MIT)

    Copyright (c) 2013 Evan Wondrasek / Apricity Software LLC
	
	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:
	
	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.
	
	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PEBakery.WPF.Controls
{
    /// <summary>
    /// Represents a view mode that displays data items in columns for a System.Windows.Controls.ListView control with auto sized columns based on the column content     
    /// </summary>
    public class AutoSizedGridView : GridView
    {
        protected override void PrepareItem(ListViewItem item)
        {
            foreach (GridViewColumn column in Columns)
            {
                // Setting NaN for the column width automatically determines the required
                // width enough to hold the content completely.

                // If the width is NaN, first set it to ActualWidth temporarily.
                if (double.IsNaN(column.Width))
                    column.Width = column.ActualWidth;

                // Finally, set the column with to NaN. This raises the property change
                // event and re computes the width.
                column.Width = double.NaN;
            }
            base.PrepareItem(item);
        }
    }

    public class PartAutoSizedGridView : GridView
    {
        protected override void PrepareItem(ListViewItem item)
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                PartAutoSizedGridViewColumn column = Columns[i] as PartAutoSizedGridViewColumn;
                if (column.ActivateAutoSize)
                {
                    // Setting NaN for the column width automatically determines the required
                    // width enough to hold the content completely.

                    // If the width is NaN, first set it to ActualWidth temporarily.
                    if (double.IsNaN(column.Width))
                        column.Width = column.ActualWidth;

                    // Finally, set the column with to NaN. This raises the property change
                    // event and re computes the width.
                    column.Width = double.NaN;
                }
            }
            base.PrepareItem(item);
        }
    }

    public class PartAutoSizedGridViewColumn : GridViewColumn
    {
        private bool activateAutoSize = true;
        public bool ActivateAutoSize
        {
            get => activateAutoSize;
            set => activateAutoSize = value;
        }
    }
}
