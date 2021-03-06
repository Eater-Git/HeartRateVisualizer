﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

using System.ServiceModel.Channels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;

using System.Collections;
using Windows.Devices.Gpio;
using Windows.ApplicationModel.Store.Preview.InstallControl;

namespace HeartRateVisualizer
{

    class HeartRateEventArgs : EventArgs
    {
        public int heart_rate;
        public DateTime datetime;
    }

    class HeartRateConnection
    {
        private GattDeviceService Service;
        private BluetoothLEAdvertisementWatcher advWatcher;
        public event EventHandler ConnectBLE;
        public event EventHandler GetHeartRate;


        public int heart_rate { get; private set; }

        public HeartRateConnection()
        {
        }

        public async void Start()
        {
            this.advWatcher = new BluetoothLEAdvertisementWatcher();
            advWatcher.Received += WathcerReceived;
            this.advWatcher.ScanningMode = BluetoothLEScanningMode.Active;  //これがないとサービスとかデバイス名の情報が得られないっぽい？
            advWatcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(700);//重さ改善
            this.advWatcher.Start();

        }

        private async void WathcerReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {

            // アドバタイズパケット受信→HeartRateサービスを検索
            bool find = false;
            var bleServiceUUIDs = args.Advertisement.ServiceUuids;


            BluetoothLEDevice dev = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
            if(dev == null)
            {
                return;
            }

            // 発見
            GattDeviceServicesResult result = await dev.GetGattServicesAsync(/*GattServiceUuids.HeartRate*/);


            if (result.Status == GattCommunicationStatus.Success)
            {
                var services = result.Services;
                foreach (var service in services)
                {
                    if (service.Uuid == GattServiceUuids.HeartRate)
                    {
                        this.Service = service;
                        find = true;
                        this.advWatcher.Stop();
                        break;
                    }
                }
            }

            //発見したデバイスがHeartRateサービスを持っていたら
            if (find)
            {
                {
                    var characteristics = await Service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.HeartRateMeasurement);



                    if(characteristics.Status == GattCommunicationStatus.Success)
                    {

                        foreach (var chr in characteristics.Characteristics)
                        {
                            if(chr.Uuid == GattCharacteristicUuids.HeartRateMeasurement)
                            {
                                this.Characteristic_HeartRate_Measurement = chr;

                                //データの送り方が二種類あるので場合分け。OH1はNotifyなのでそちら側しか動作確認をしていない
                                if (this.Characteristic_HeartRate_Measurement.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                                {
                                    this.Characteristic_HeartRate_Measurement.ValueChanged += characteristicChanged_HeartRate_Measurement;
                                    await this.Characteristic_HeartRate_Measurement.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                                }
                                if (this.Characteristic_HeartRate_Measurement.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                                {
                                    this.Characteristic_HeartRate_Measurement.ValueChanged += characteristicChanged_HeartRate_Measurement;
                                    await this.Characteristic_HeartRate_Measurement.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                }
                                OnConnectBLE(EventArgs.Empty);
                                break;

                            }

                        }

                    }
                    else
                    {
                        this.advWatcher.Start();
                    }
                }

            }
        }


        private GattCharacteristic Characteristic_HeartRate_Measurement;

        private void characteristicChanged_HeartRate_Measurement(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] data = new byte[eventArgs.CharacteristicValue.Length];
            Windows.Storage.Streams.DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(data);
            heart_rate = data[1];
            HeartRateEventArgs arg = new HeartRateEventArgs();
            arg.heart_rate = heart_rate;
            arg.datetime = DateTime.Now;
            OnGetHeartRate(arg);
            return;
        }

        protected virtual void OnConnectBLE(EventArgs e)
        {
            ConnectBLE?.Invoke(this, e);
        }

        protected virtual void OnGetHeartRate(EventArgs e)
        {
            GetHeartRate?.Invoke(this, e);
        }
    }
}
