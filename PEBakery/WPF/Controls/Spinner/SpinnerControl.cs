/*
    Copyright 2012 lapthorn.net.

    This software is provided "as is" without a warranty of any kind.All
    express or implied conditions, representations and warranties, including
    any implied warranty of merchantability, fitness for a particular purpose
    or non-infringement, are hereby excluded.lapthorn.net and its licensors
    shall not be liable for any damages suffered by licensee as a result of
    using the software.In no event will lapthorn.net be liable for any
   lost revenue, profit or data, or for direct, indirect, special,
    consequential, incidental or punitive damages, however caused and regardless
    of the theory of liability, arising out of the use of or inability to use
    software, even if lapthorn.net has been advised of the possibility of
    such damages.

    From https://www.codeproject.com/Articles/315461/A-WPF-Spinner-Custom-Control v1.02
    Licensed under The Code Project Open License (CPOL) 1.02
    CPOL 1.02 : https://www.codeproject.com/info/cpol10.aspx

    Created by Barry Lapthorn, Modified by Hajin Jang
*/

using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PEBakery.WPF.Controls
{
    /// <summary>
    /// Interaction logic for SpinnerControl.xaml
    /// </summary>
    public class SpinnerControl : UserControl
    {
        public SpinnerControl()
        {
        }

        static SpinnerControl()
        {
            InitializeCommands();

            DefaultStyleKeyProperty.OverrideMetadata(typeof(SpinnerControl), new FrameworkPropertyMetadata(typeof(SpinnerControl)));
        }

        #region Value property
        /// <summary>
        /// This is the Control property that we expose to the user.
        /// </summary>
        [Category("SpinnerControl")]
        public decimal Value
        {
            get { return (decimal)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        private static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(decimal), typeof(SpinnerControl),
            new FrameworkPropertyMetadata(DefaultValue,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged,
                CoerceValue
                ));

        /// <summary>
        /// If the value changes, update the text box that displays the Value 
        /// property to the consumer.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="args"></param>
        private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (obj is SpinnerControl control)
            {
                var newValue = (decimal)args.NewValue;
                var oldValue = (decimal)args.OldValue;

                RoutedPropertyChangedEventArgs<decimal> e =
                    new RoutedPropertyChangedEventArgs<decimal>(oldValue, newValue, ValueChangedEvent);

                control.OnValueChanged(e);
            }
        }

        /// <summary>
        /// Raise the ValueChanged event.  Derived classes can use this.
        /// </summary>
        /// <param name="e"></param>
        virtual protected void OnValueChanged(RoutedPropertyChangedEventArgs<decimal> e)
        {
            RaiseEvent(e);
        }

        private static decimal LimitValueByBounds(decimal newValue, SpinnerControl control)
        {
            newValue = Math.Max(control.Minimum, Math.Min(control.Maximum, newValue));
            //  then ensure the number of decimal places is correct.
            newValue = Decimal.Round(newValue, control.DecimalPlaces);
            return newValue;
        }

        private static object CoerceValue(DependencyObject obj, object value)
        {
            decimal newValue = (decimal)value;

            if (obj is SpinnerControl control)
            {
                //  ensure that the value stays within the bounds of the minimum and
                //  maximum values that we define.
                newValue = LimitValueByBounds(newValue, control);
            }

            return newValue;
        }


        #endregion


        #region MinimumValue property
        /// <summary>
        /// This is the Control property that we expose to the user.
        /// </summary>
        [Category("SpinnerControl")]
        public decimal Minimum
        {
            get { return (decimal)GetValue(MinimumValueProperty); }
            set { SetValue(MinimumValueProperty, value); }
        }

        private static readonly DependencyProperty MinimumValueProperty =
            DependencyProperty.Register("Minimum", typeof(decimal), typeof(SpinnerControl),
            new PropertyMetadata(DefaultMinimumValue));
        #endregion

        
        #region MaximumValue property
        /// <summary>
        /// This is the Control property that we expose to the user.
        /// </summary>
        [Category("SpinnerControl")]
        public decimal Maximum
        {
            get { return (decimal)GetValue(MaximumValueProperty); }
            set { SetValue(MaximumValueProperty, value); }
        }

        private static readonly DependencyProperty MaximumValueProperty =
            DependencyProperty.Register("Maximum", typeof(decimal), typeof(SpinnerControl),
            new PropertyMetadata(DefaultMaximumValue));

        #endregion


        #region Button, Brush's Width, Height property
        /// <summary>
        /// This is the Control property that we expose to the user.
        /// </summary>
        [Category("SpinnerControl")]
        public double ButtonWidth
        {
            get => Height;
        }

        [Category("SpinnerControl")]
        public double ButtonHeight
        {
            get => Height / 2;
        }

        [Category("SpinnerControl")]
        public double ArrowBrushWidth
        {
            get => Height * 0.6;
        }

        [Category("SpinnerControl")]
        public double ArrowBrushHeight
        {
            get => Height * 0.35;
        }

        #endregion


        #region DecimalPlaces property
        /// <summary>
        /// This is the Control property that we expose to the user.
        /// </summary>
        [Category("SpinnerControl")]
        public int DecimalPlaces
        {
            get { return (int)GetValue(DecimalPlacesProperty); }
            set { SetValue(DecimalPlacesProperty, value); }
        }

        private static readonly DependencyProperty DecimalPlacesProperty =
            DependencyProperty.Register("DecimalPlaces", typeof(int), typeof(SpinnerControl),
            new PropertyMetadata(DefaultDecimalPlaces));

        #endregion


        #region Change property
        /// <summary>
        /// This is the Control property that we expose to the user.
        /// </summary>
        [Category("SpinnerControl")]
        public decimal Change
        {
            get { return (decimal)GetValue(ChangeProperty); }
            set { SetValue(ChangeProperty, value); }
        }

        private static readonly DependencyProperty ChangeProperty =
            DependencyProperty.Register("Change", typeof(decimal), typeof(SpinnerControl),
            new PropertyMetadata(DefaultChange));

        #endregion


        #region Default values

        /// <summary>
        /// Define the min, max and starting value, which we then expose 
        /// as dependency properties.
        /// </summary>
        private const Decimal DefaultMinimumValue = 0,
            DefaultMaximumValue = int.MaxValue,
            DefaultValue = DefaultMinimumValue,
            DefaultChange = 1;

        /// <summary>
        /// The default number of decimal places, i.e. 0, and show the
        /// spinner control as an int initially.
        /// </summary>
        private const int DefaultDecimalPlaces = 0;
        #endregion


        #region Command Stuff
        public static RoutedCommand IncreaseCommand { get; set; }

        protected static void OnIncreaseCommand(Object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is SpinnerControl control)
            {
                control.OnIncrease();
            }
        }

        protected void OnIncrease()
        {
            //  see https://connect.microsoft.com/VisualStudio/feedback/details/489775/
            //  for why we do this.
            Value = LimitValueByBounds(Value + Change, this);
        }

        public static RoutedCommand DecreaseCommand { get; set; }

        protected static void OnDecreaseCommand(Object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is SpinnerControl control)
            {
                control.OnDecrease();
            }
        }

        protected void OnDecrease()
        {
            //  see https://connect.microsoft.com/VisualStudio/feedback/details/489775/
            //  for why we do this.
            Value = LimitValueByBounds(Value - Change, this);
        }

        /// <summary>
        /// Since we're using RoutedCommands for the up/down buttons, we need to
        /// register them with the command manager so we can tie the events
        /// to callbacks in the control.
        /// </summary>
        private static void InitializeCommands()
        {
            //  create instances
            IncreaseCommand = new RoutedCommand("IncreaseCommand", typeof(SpinnerControl));
            DecreaseCommand = new RoutedCommand("DecreaseCommand", typeof(SpinnerControl));

            //  register the command bindings - if the buttons get clicked, call these methods.
            CommandManager.RegisterClassCommandBinding(typeof(SpinnerControl), new CommandBinding(IncreaseCommand, OnIncreaseCommand));
            CommandManager.RegisterClassCommandBinding(typeof(SpinnerControl), new CommandBinding(DecreaseCommand, OnDecreaseCommand));

            //  lastly bind some inputs:  i.e. if the user presses up/down arrow 
            //  keys, call the appropriate commands.
            CommandManager.RegisterClassInputBinding(typeof(SpinnerControl), new InputBinding(IncreaseCommand, new KeyGesture(Key.Up)));
            CommandManager.RegisterClassInputBinding(typeof(SpinnerControl), new InputBinding(IncreaseCommand, new KeyGesture(Key.Right)));
            CommandManager.RegisterClassInputBinding(typeof(SpinnerControl), new InputBinding(DecreaseCommand, new KeyGesture(Key.Down)));
            CommandManager.RegisterClassInputBinding(typeof(SpinnerControl), new InputBinding(DecreaseCommand, new KeyGesture(Key.Left)));
        }
        #endregion


        #region Events

        /// <summary>
        /// The ValueChangedEvent, raised if  the value changes.
        /// </summary>
        private static readonly RoutedEvent ValueChangedEvent = 
            EventManager.RegisterRoutedEvent("ValueChanged", RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<decimal>), typeof(SpinnerControl));

        /// <summary>
        /// Occurs when the Value property changes.
        /// </summary>
        public event RoutedPropertyChangedEventHandler<decimal> ValueChanged
        {
            add { AddHandler(ValueChangedEvent, value); }
            remove { RemoveHandler(ValueChangedEvent, value); }
        }
        #endregion
    }
}
