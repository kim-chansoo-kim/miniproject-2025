using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MQTTnet;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WpfIotSimulatorApp.Models;

namespace WpfIotSimulatorApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        #region MQTT 재접속용 변수
        private Timer _mqttMonitorTimer;
        private bool _isReconnecting = false;
        #endregion
        private string _greeting;
        // 색상표시할 변수
        private Brush _productBrush;
        private string _logText; // 로그출력

        #region 뷰와 관계없는 멤버 변수

        private IMqttClient mqttClient;
        private string brokerHost;
        private string mqttPubTopic;
        private string mqttSubTopic;
        private string clientId;

        private int logNum;
        private MqttClientOptions options;

        #endregion

        #region 생성자
        public MainViewModel()
        {
            Greeting = "IoT Sorting Simulator";
            LogText = "프로그램 실행";
            // MQTT용 초기화
            brokerHost = "210.119.12.58";
            clientId = "IoT18"; // IoT장비번호
            mqttPubTopic = "pknu/sf58/data"; // 스마트팩토리 토픽
            mqttSubTopic = "pknu/sf58/control";
            logNum = 1;
            InitMqttClient();

            // MQTT 재접속 확인용 타이머 실행
            StartMqttMonitor();
        }

        

        public string Greeting
        {
            get => _greeting;
            set => SetProperty(ref _greeting, value);
        }
        #endregion

        #region 뷰와 연계되는 속성
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        // 제품 배경색 바인딩 속성
        public Brush ProductBrush
        {
            get => _productBrush;
            set => SetProperty(ref _productBrush, value);
        }

        #endregion
        #region 일반메서드
        private void StartMqttMonitor()
        {
            _mqttMonitorTimer = new Timer(async _ =>
            {
                await CheckMqttConnectionAsync();
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
        }

        // 핵심. MQTTClinet 접속이 끊어지면 재접속
        private async Task CheckMqttConnectionAsync()
        {
            if (!mqttClient.IsConnected)
            {
                _isReconnecting = true;
                LogText = "MQTT 연결해제. 재접속중...";
                try
                {
                    // MQTT 클라이언트 접속 설정
                    var mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithTcpServer(brokerHost, 1883)
                        .WithClientId(clientId)
                        .WithCleanSession(true)
                        .Build();

                    await mqttClient.ConnectAsync(options);
                    LogText = "MQTT 재접속 성공!";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MQTT 재접속 실패 : {ex.Message}");
                }
            }
        }

        private async Task InitMqttClient()
        {
            var mqttFactory = new MqttClientFactory();
            mqttClient = mqttFactory.CreateMqttClient();

            // MQTT 클라이언트 접속 설정
            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, 1883)
                .WithClientId(clientId)
                .WithCleanSession(true)
                .Build();

            // MQTT 클라이언트에 접속
            mqttClient.ConnectedAsync += async e =>
            {
                LogText = "MQTT 브로커 접속성공!!";
            };

            await mqttClient.ConnectAsync(mqttClientOptions);

            // 테스트 메시지
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(mqttPubTopic)
                .WithPayload("Hello From IoT Simulator!")
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();
            // MQTT 브로커로 전송
            await mqttClient.PublishAsync(message);
            LogText = "MQTT 브로커에 메시지성공!!";

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(mqttSubTopic).Build());
            mqttClient.ApplicationMessageReceivedAsync += MqttMessageReceivedAsync;
        }

        private Task MqttMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);

            var data = JsonConvert.DeserializeObject<PrcMsg>(payload);

            //LogText = data.Flag;
            if (data.Flag.ToUpper() == "ON")
            {
                Move();
                Thread.Sleep(2000);
                Check();
            }

            return Task.CompletedTask;
        }
        #endregion
        #region 이벤트 영역
        public event Action? StartHmiRequested;
        public event Action? StartSensorCheckRequested; // VM에서 View에 있는 이벤트를 호출
        #endregion

        #region 릴레이커멘드 영역
        [RelayCommand]
        public void Move()
        {
            ProductBrush = Brushes.DeepPink;
            Application.Current.Dispatcher.Invoke(() =>
            {
                StartHmiRequested?.Invoke(); // 컨베이어벨트 애니메이션 요청 (View에서 처리)
            });
        }

        [RelayCommand]
        public void Check()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StartSensorCheckRequested?.Invoke();
            });

            // 양품/불량품 판단
            Random rand = new();
            int result = rand.Next(1, 3);

            /*
            switch (result)
            {
                case 1:
                    ProductBrush = Brushes.Green;
                    break;
                case 2:
                    ProductBrush = Brushes.Crimson;
                    break;
                default:
                    ProductBrush = Brushes.Aqua;
                    break;
            } // 아래의 람다 switch와 완전동일 기능  */

            ProductBrush = result switch
            {
                1 => Brushes.GreenYellow, // 양품
                2 => Brushes.Crimson, // 불량
                _ => Brushes.BlueViolet,
            };
            #endregion
            // MQTT로 데이터 전송
            var resultText = result == 1 ? "OK" : "FAIL";
            var payload = new CheckResult
            {
                ClientId = clientId,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Result = resultText,
            };
            var jsonPayload = JsonConvert.SerializeObject(payload, Formatting.Indented);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(mqttPubTopic)
                .WithPayload(jsonPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();
            // MQTT 브로커로 전송
            mqttClient.PublishAsync(message);
            LogText = $"MQTT 브로커에 결과 메시지 전송!! : {logNum++}";
        }
    }
}
