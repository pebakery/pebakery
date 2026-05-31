/*
    MIT License (MIT)

    Copyright (C) 2018-present Hajin Jang
	
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PEBakery.Core.WpfControls
{
    public partial class NumberBox : UserControl
    {
        #region Constructor
        public NumberBox()
        {
            InitializeComponent();
        }
        #endregion

        #region Property
        private const decimal DefaultValue = 0;
        public decimal Value
        {
            get => (decimal)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(decimal), typeof(NumberBox),
            new FrameworkPropertyMetadata(DefaultValue, OnValueChanged, CoerceValue));

        private const decimal DefaultMinimum = 0;
        public decimal Minimum
        {
            get => (decimal)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(decimal), typeof(NumberBox),
            new FrameworkPropertyMetadata(DefaultMinimum, OnConstraintChanged));

        private const decimal DefaultMaximum = ushort.MaxValue;
        public decimal Maximum
        {
            get => (decimal)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(decimal), typeof(NumberBox),
            new FrameworkPropertyMetadata(DefaultMaximum, OnConstraintChanged));

        private const decimal DefaultIncrementUnit = 1;
        public decimal IncrementUnit
        {
            get => (decimal)GetValue(IncrementUnitProperty);
            set => SetValue(IncrementUnitProperty, value);
        }

        public static readonly DependencyProperty IncrementUnitProperty = DependencyProperty.Register("IncrementUnit", typeof(decimal), typeof(NumberBox),
            new FrameworkPropertyMetadata(DefaultIncrementUnit));

        private const int DefaultDecimalPlaces = 0;
        public int DecimalPlaces
        {
            get => (int)GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.Register("DecimalPlaces", typeof(int), typeof(NumberBox),
            new FrameworkPropertyMetadata(DefaultDecimalPlaces));
        #endregion

        #region Callbacks
        private static object CoerceValue(DependencyObject element, object value)
        {
            // Check if (MinValue <= Value <= MaxValue)
            if (element is NumberBox control)
                return LimitDecimalValue(control, (decimal)value);
            return value;
        }

        private static void OnConstraintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Re-run CoerceValue so Value is always within the current [Minimum, Maximum],
            // range regardless of which order the properties were assigned.
            d.CoerceValue(ValueProperty);
        }

        private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            NumberBox control = (NumberBox)obj;

            RoutedPropertyChangedEventArgs<decimal> e = new RoutedPropertyChangedEventArgs<decimal>(
                (decimal)args.OldValue, (decimal)args.NewValue, ValueChangedEvent);
            control.OnValueChanged(e);
        }

        public static decimal LimitDecimalValue(NumberBox control, decimal value)
        {
            value = Math.Max(control.Minimum, Math.Min(control.Maximum, value));
            value = decimal.Round(value, control.DecimalPlaces);
            return value;
        }
        #endregion

        #region Control Events
        public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
            "ValueChanged", RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<decimal>), typeof(NumberBox));

        public event RoutedPropertyChangedEventHandler<decimal> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        protected virtual void OnValueChanged(RoutedPropertyChangedEventArgs<decimal> args)
        {
            RaiseEvent(args);
        }
        #endregion

        #region TextBlock Events
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = (TextBox)sender;

            // Determine the full text that would result from accepting this input,
            // also take into account any selected text that would be overwritten.
            string proposedValue = textBox.Text
                .Remove(textBox.SelectionStart, textBox.SelectionLength)
                .Insert(textBox.SelectionStart, e.Text);

            // Allow an optional leading '-' (only when Minimum < 0),
            // followed by zero or more digits. Also allow a single '-' as a transient
            // state while the user is still typing the rest of the number.
            bool check = proposedValue.Length > 0;
            if (check)
            {
                int i = 0;
                if (proposedValue[0] == '-')
                {
                    if (Minimum >= 0)   // negative values are impossible in this situation so reject the '-'
                        check = false;
                    else
                        i = 1;          // '-' is valid; make sure the rest are digits
                }
                for (; check && i < proposedValue.Length; i++)
                    check = char.IsDigit(proposedValue[i]);
            }

            e.Handled = !check;

            OnPreviewTextInput(e);
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Add event for Up and Down
            switch (e.Key)
            {
                case Key.Up:
                    Value = LimitDecimalValue(this, Value + IncrementUnit);
                    break;
                case Key.Down:
                    Value = LimitDecimalValue(this, Value - IncrementUnit);
                    break;
            }
        }
        #endregion

        #region Button Events
        private void UpButton_Click(object sender, EventArgs e)
        {
            Value = LimitDecimalValue(this, Value + IncrementUnit);
        }

        private void DownButton_Click(object sender, EventArgs e)
        {
            Value = LimitDecimalValue(this, Value - IncrementUnit);
        }
        #endregion
    }
}
