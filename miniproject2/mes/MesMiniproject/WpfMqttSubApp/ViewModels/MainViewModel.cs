using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.Controls.Dialogs;
using MQTTnet;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Windows.Threading;
using WpfIotSimulatorApp.Models;
using WpfMqttSubApp.Models;


namespace WpfMqttSubApp.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        #region 내부 멤버변수

        private IMqttClient mqttClient;
        private readonly IDialogCoordinator dialogCoordinator;
        private readonly DispatcherTimer timer;
        private int counter = 1; // TODO : 나중에 텍스트가 너무 많아져서 느려지면 초기화시 사용
        
        private string connString = string.Empty;
        private MySqlConnection connection;
        private string mqttTopic;
        private string clientId;

        #endregion

        #region MVVM용 멤버변수

        private string _brokerHost;
        private string _databaseHost;
        private string _logText;

        #endregion

        #region 생성자
        // 속성 BrokerHost, DatabaseHost
        // 메서드 ConnectBroker, ConnectDatabase

        public MainViewModel(IDialogCoordinator coordinator)
        {
            this.dialogCoordinator = coordinator;

            BrokerHost = App.Configuration.Mqtt.Broker;
            DatabaseHost = App.Configuration.Database.Server;
            mqttTopic = App.Configuration.Mqtt.Topic; // 설정파일로 작업가능
            clientId = App.Configuration.Mqtt.ClientId;

            connection = new MySqlConnection(); // 예외 처리용

            // RichTextBox 테스트용
            //timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromSeconds(1);
            //timer.Tick += (sender, e) =>
            //{
            //    // ReachTextBox 추가내용
            //    LogText += $"Log [{DateTime.Now:HH:mm:ss}] - {counter++}\n";
            //    Debug.WriteLine($"Log [{DateTime.Now:HH:mm:ss}] - {counter++}");
            //};
            //timer.Start();
        }

        #endregion

        #region MVVM 속성
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        public string BrokerHost 
        {
            get => _brokerHost;
            set => SetProperty(ref _brokerHost, value);
        }

        public string DatabaseHost
        {
            get => _databaseHost;
            set => SetProperty(ref _databaseHost, value);
        }

        #endregion

        private async Task ConnectMqttBroker()
        {
            // MQTT 클라이언트 생성
            var mqttFactory = new MqttClientFactory();
            mqttClient = mqttFactory.CreateMqttClient();

            // MQTT 클라이언트 접속 설정
            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(BrokerHost)
                .WithClientId(clientId) // 구독시스템도 클라이언트ID가 필요할 수 있음
                .WithCleanSession(true)
                .Build();
            
            // MQTT 접속 후 이벤트 처리
            mqttClient.ConnectedAsync += async e =>
            {
                LogText += "MQTT 브로커 접속성공!\n";
                // 연결 이후 구독(Subscribe)
                await mqttClient.SubscribeAsync(mqttTopic);
            };
            // MQTT 구독메시지 로그출력
            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = e.ApplicationMessage.ConvertPayloadToString(); // byte데이터를 UTF-8 문자열로 변환

                // json데이터를 일반객체로 다시 변환 -> 역직렬화(Deserialization)
                var data = JsonConvert.DeserializeObject<CheckResult>(payload);
                Debug.WriteLine($"{data.ClientId} / {data.Timestamp} / {data.Result}");

                //SaveSensingData(data);

                LogText += $"LineCounter : {counter}\n";
                LogText += $"{payload}\n";

                return Task.CompletedTask;
            };

            // MQTT 서버에 접속
            await mqttClient.ConnectAsync(mqttClientOptions); 
        }

        private async Task SaveSensingData(FakeInfo data)
        {
            string query = @"INSERT INTO fakedatas
                                    (sensing_dt, pub_id, count,
                                     temp, humid, light, human)
                            VALUES
                                    (@sensing_dt, @pub_id, @count, 
                                     @temp, @humid, @light, @human)";
            try
            {
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    using var cmd = new MySqlCommand(query, connection);
                    cmd.Parameters.AddWithValue("@sensing_dt", data.Sensing_Dt);
                    cmd.Parameters.AddWithValue("@pub_id", data.Pub_Id);
                    cmd.Parameters.AddWithValue("@count", data.Count);
                    cmd.Parameters.AddWithValue("@temp", data.Temp);
                    cmd.Parameters.AddWithValue("@humid", data.Humid);
                    cmd.Parameters.AddWithValue("@light", data.Light);
                    cmd.Parameters.AddWithValue("@human", data.Human);

                    await cmd.ExecuteNonQueryAsync(); // 이전까지는 cmd.Execute
                }
            }
            catch (Exception ex)
            {
                // TODO : 아무 예외처리도 안해도 됨
            }
        }

        private async Task ConnectDatabaseServer()
        {
            try
            {
                connection = new MySqlConnection(connString);
                connection.Open();
                LogText += $"{DatabaseHost} DB접속 성공! {connection.State}\n";
            }
            catch (Exception ex)
            {
                LogText += $"{DatabaseHost} DB접속 실패! : {ex.Message} \n";
            }
        }

        [RelayCommand]
        public async Task ConnectBroker()
        {
            if (string.IsNullOrEmpty(BrokerHost))
            {
                await this.dialogCoordinator.ShowMessageAsync(this, "브로커 연결", "브로커 연결합니다!");
                return;
            }
            // MQTT브로커에 접속해서 데이터를 가져오기
            await ConnectMqttBroker();
        }

        [RelayCommand]
        public async Task ConnectDatabase()
        {
            if (string.IsNullOrEmpty(DatabaseHost))
            {
                await this.dialogCoordinator.ShowMessageAsync(this, "DB 연결", "DB 호스트를 입력하세요");
                return;
            }
            connString = $"Server={DatabaseHost};Database={App.Configuration.Database.Database};Uid={App.Configuration.Database.UserId};Pwd={App.Configuration.Database.Password};Charset=utf8";

            await ConnectDatabaseServer();
        }

        public void Dispose()
        {
            // 리소스 해제를 명시적으로 처리하는 기능 추가
            connection?.Close(); // DB접속 해제
        }
    }
}
