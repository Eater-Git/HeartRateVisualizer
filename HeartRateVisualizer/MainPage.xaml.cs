using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LiveCharts;
using LiveCharts.Configurations;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.ComponentModel;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください
namespace HeartRateVisualizer
{
    public class MeasureModel
    {
        public DateTime DateTime { get; set; }
        public double Value { get; set; }
    }
}
namespace HeartRateVisualizer
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private double _axisMax;
        private double _axisMin;
        private double _BPMaxisMax;
        private double _BPMaxisMin;
        private HeartRateConnection OH1;

        public MainPage()
        {
            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => { return model != null ? model.DateTime.Ticks : DateTime.Now.Ticks; })   //X軸の設定　nullなら現在時刻
                .Y(model => { return model != null ? model.Value : 0; });           //Y軸の設定　nullなら0

            
            Charting.For<MeasureModel>(mapper);


            ChartValues = new ChartValues<MeasureModel>();

            //軸ラベルの設定
            DateTimeFormatter = value => new DateTime((long)(value)).ToString("mm:ss");
            BPMFormatter = value => ((long)value).ToString("D");

            //X軸の目盛りの設定
            AxisStep = TimeSpan.FromSeconds(30).Ticks;
            SetAxisLimits(DateTime.Now);

            //Y軸の目盛りの設定
            BPMAxisStep = 10;
            BPMAxisMax = 150;
            BPMAxisMin = 50;


            DataContext = this;

            //BLE通信
            OH1 = new HeartRateConnection();
            OH1.ConnectBLE += ShowGraph;
            OH1.Start();


        }

        private void ShowGraph(object sender, object e) //BLE通信が確立したときに呼ばれる
        {

            OH1.GetHeartRate += AddPlot;
        }
        private async void AddPlot(object sender, object e) //センサから値を受け取った時に呼ばれる
        {
            if (e == null)
                return;
            MeasureModel mm = new MeasureModel();
            mm.DateTime = ((HeartRateEventArgs)e).datetime;
            mm.Value = ((HeartRateEventArgs)e).heart_rate;

            if (mm.DateTime == null)
                return;

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => ChartValues.Add(mm)); //UIスレッドからしか呼べないらしい
            

            SetAxisLimits(((HeartRateEventArgs)e).datetime);    //X軸を更新

            if (ChartValues[0] == null) //ﾇﾙﾎﾟ回避
                return;
            if(ChartValues[0].DateTime.Ticks < AxisMin)
                ChartValues.RemoveAt(0);

        }

        public ChartValues<MeasureModel> ChartValues { get; set; }
        public Func<double, string> DateTimeFormatter { get; set; }
        public Func<double, string> BPMFormatter { get; set; }

        public double AxisStep { get; set; }
        public double BPMAxisStep { get; set; }

        public double AxisMax
        {
            get { return _axisMax; }
            set
            {
                _axisMax = value;
                OnPropertyChanged("AxisMax");
            }
        }
        public double AxisMin
        {
            get { return _axisMin; }
            set
            {
                _axisMin = value;
                OnPropertyChanged("AxisMin");
            }
        }
        public double BPMAxisMax
        {
            get { return _BPMaxisMax; }
            set
            {
                _BPMaxisMax = value;
                OnPropertyChanged("BPMAxisMax");
            }
        }
        public double BPMAxisMin
        {
            get { return _BPMaxisMin; }
            set
            {
                _BPMaxisMin = value;
                OnPropertyChanged("BPMAxisMin");
            }
        }

        private void SetAxisLimits(DateTime now)
        {
            AxisMax = now.Ticks + TimeSpan.FromSeconds(1).Ticks; 
            AxisMin = now.Ticks - TimeSpan.FromSeconds(120).Ticks; //X軸の範囲は120秒
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public async void OnPropertyChanged(string propertyName = null)
        {
            if (PropertyChanged != null)
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName)));

        }
    }
}
