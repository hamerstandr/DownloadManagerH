using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DownloadManager.Controls
{
    /// <summary>
    /// کنترل ProgressBar سفارشی با نمایش درصد در وسط
    /// </summary>
    public partial class ProgressBarWithText : UserControl
    {
        #region Dependency Properties

        /// <summary>
        /// مقدار پیشرفت (0-100)
        /// </summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(ProgressBarWithText),
                new PropertyMetadata(0.0, OnValueChanged));

        /// <summary>
        /// حداکثر مقدار
        /// </summary>
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ProgressBarWithText),
                new PropertyMetadata(100.0, OnValueChanged));

        /// <summary>
        /// حداقل مقدار
        /// </summary>
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ProgressBarWithText),
                new PropertyMetadata(0.0, OnValueChanged));

        ///// <summary>
        ///// متن درصد
        ///// </summary>
        //public static readonly DependencyProperty PercentageTextProperty =
        //    DependencyProperty.Register(nameof(PercentageText), typeof(string), typeof(ProgressBarWithText),
        //        new PropertyMetadata("0%"));

        /// <summary>
        /// نمایش متن درصد
        /// </summary>
        public static readonly DependencyProperty ShowPercentageProperty =
            DependencyProperty.Register(nameof(ShowPercentage), typeof(bool), typeof(ProgressBarWithText),
                new PropertyMetadata(true, OnShowPercentageChanged));

        /// <summary>
        /// رنگ متن
        /// </summary>
        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(nameof(TextColor), typeof(Brush), typeof(ProgressBarWithText),
                new PropertyMetadata(Brushes.White));

        /// <summary>
        /// اندازه فونت متن
        /// </summary>
        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register(nameof(TextFontSize), typeof(double), typeof(ProgressBarWithText),
                new PropertyMetadata(11.0));

        /// <summary>
        /// افکت متن (سایه)
        /// </summary>
        public static readonly DependencyProperty TextEffectProperty =
            DependencyProperty.Register(nameof(TextEffect), typeof(Effect), typeof(ProgressBarWithText),
                new PropertyMetadata(new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 2,
                    ShadowDepth = 1,
                    Opacity = 0.7
                }));

        /// <summary>
        /// رنگ پیشرفت بر اساس درصد
        /// </summary>
        public static readonly DependencyProperty UseGradientColorProperty =
            DependencyProperty.Register(nameof(UseGradientColor), typeof(bool), typeof(ProgressBarWithText),
                new PropertyMetadata(true, OnValueChanged));

        #endregion

        #region Properties

        /// <summary>
        /// مقدار پیشرفت
        /// </summary>
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// حداکثر مقدار
        /// </summary>
        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>
        /// حداقل مقدار
        /// </summary>
        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        ///// <summary>
        ///// متن درصد
        ///// </summary>
        //public string PercentageText
        //{
        //    get => (string)GetValue(PercentageTextProperty);
        //    set => SetValue(PercentageTextProperty, value);
        //}

        /// <summary>
        /// نمایش متن درصد
        /// </summary>
        public bool ShowPercentage
        {
            get => (bool)GetValue(ShowPercentageProperty);
            set => SetValue(ShowPercentageProperty, value);
        }

        /// <summary>
        /// رنگ متن
        /// </summary>
        public Brush TextColor
        {
            get => (Brush)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        /// <summary>
        /// اندازه فونت متن
        /// </summary>
        public double TextFontSize
        {
            get => (double)GetValue(TextFontSizeProperty);
            set => SetValue(TextFontSizeProperty, value);
        }

        /// <summary>
        /// افکت متن
        /// </summary>
        public Effect TextEffect
        {
            get => (Effect)GetValue(TextEffectProperty);
            set => SetValue(TextEffectProperty, value);
        }

        /// <summary>
        /// استفاده از رنگ گرادیان
        /// </summary>
        public bool UseGradientColor
        {
            get => (bool)GetValue(UseGradientColorProperty);
            set => SetValue(UseGradientColorProperty, value);
        }

        #endregion

        public ProgressBarWithText()
        {
            InitializeComponent();
            UpdateProgressBar();
        }

        #region Event Handlers

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressBarWithText control)
            {
                // به‌روزرسانی ProgressBar و متن درصد
                control.UpdateProgressBar();
            }
        }

        private static void OnShowPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressBarWithText control)
            {
                control.PercentageText.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// به‌روزرسانی ProgressBar
        /// </summary>
        private void UpdateProgressBar()
        {
            if (MainProgressBar == null) return;

            // محاسبه درصد
            var range = Maximum - Minimum;
            var percentage = range > 0 ? ((Value - Minimum) / range) * 100 : 0;
            percentage = Math.Max(0, Math.Min(100, percentage));

            // تنظیم مقدار ProgressBar
            MainProgressBar.Value = Value;
            MainProgressBar.Maximum = Maximum;
            MainProgressBar.Minimum = Minimum;

            // تنظیم متن درصد
            PercentageText.Text = $"{Value:F0}%";

            // تنظیم رنگ بر اساس درصد
            if (UseGradientColor)
            {
                MainProgressBar.Foreground = GetProgressColor(percentage);
            }

            // تنظیم رنگ متن بر اساس پس‌زمینه
            UpdateTextColor(percentage);
        }

        /// <summary>
        /// دریافت رنگ پیشرفت بر اساس درصد
        /// </summary>
        private static Brush GetProgressColor(double percentage)
        {
            if (percentage < 25)
            {
                // قرمز برای درصد کم
                return new SolidColorBrush(Color.FromRgb(220, 53, 69)); // #dc3545
            }
            else if (percentage < 50)
            {
                // نارنجی برای درصد متوسط
                return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // #ffc107
            }
            else if (percentage < 75)
            {
                // آبی برای درصد خوب
                return new SolidColorBrush(Color.FromRgb(0, 123, 255)); // #007bff
            }
            else
            {
                // سبز برای درصد عالی
                return new SolidColorBrush(Color.FromRgb(40, 167, 69)); // #28a745
            }
        }

        /// <summary>
        /// به‌روزرسانی رنگ متن بر اساس پس‌زمینه
        /// </summary>
        private void UpdateTextColor(double percentage)
        {
            if (PercentageText == null) return;

            // اگر درصد کم است، متن را تیره کن
            if (percentage < 30)
            {
                PercentageText.Foreground = new SolidColorBrush(Colors.Black);
                PercentageText.Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = 2,
                    ShadowDepth = 1,
                    Opacity = 0.7
                };
            }
            else
            {
                PercentageText.Foreground = TextColor;
                PercentageText.Effect = TextEffect;
            }

            PercentageText.FontSize = TextFontSize;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// تنظیم مقدار پیشرفت با انیمیشن
        /// </summary>
        public void SetValueWithAnimation(double newValue, TimeSpan duration)
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = Value,
                To = newValue,
                Duration = duration,
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase()
            };

            BeginAnimation(ValueProperty, animation);
        }

        /// <summary>
        /// ریست کردن پیشرفت
        /// </summary>
        public void Reset()
        {
            Value = Minimum;
        }

        /// <summary>
        /// تکمیل پیشرفت
        /// </summary>
        public void Complete()
        {
            Value = Maximum;
        }

        #endregion
    }
}