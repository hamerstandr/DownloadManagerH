using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DownloadManagerH.Controls
{
    public partial class NumericUpDown : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinProperty = DependencyProperty.Register(
            nameof(Min), typeof(int), typeof(NumericUpDown), new PropertyMetadata(0));

        public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
            nameof(Max), typeof(int), typeof(NumericUpDown), new PropertyMetadata(5));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        public int Min
        {
            get => (int)GetValue(MinProperty);
            set => SetValue(MinProperty, value);
        }
        public int Max
        {
            get => (int)GetValue(MaxProperty);
            set => SetValue(MaxProperty, value);
        }

        public NumericUpDown()
        {
            InitializeComponent();
            if (Value < Min) Value = Min;
            txtValue.Text = Value.ToString();
            txtValue.PreviewTextInput += TxtValue_PreviewTextInput;
            txtValue.LostFocus += TxtValue_LostFocus;
            DataObject.AddPastingHandler(txtValue, OnPaste);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumericUpDown ctrl)
            {
                int v = (int)e.NewValue;
                if (v < ctrl.Min) v = ctrl.Min;
                if (v > ctrl.Max) v = ctrl.Max;
                ctrl.txtValue.Text = v.ToString();
            }
        }

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        {
            if (Value < Max) Value++;
        }
        private void BtnDown_Click(object sender, RoutedEventArgs e)
        {
            if (Value > Min) Value--;
        }
        private void TxtValue_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }
        private void TxtValue_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtValue.Text, out int v))
            {
                if (v < Min) v = Min;
                if (v > Max) v = Max;
                Value = v;
            }
            else
            {
                txtValue.Text = Value.ToString();
            }
        }
        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!int.TryParse(text, out _))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
} 