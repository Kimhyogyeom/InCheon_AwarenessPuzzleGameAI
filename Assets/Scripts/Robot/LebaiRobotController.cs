using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LebaiRobotController : MonoBehaviour
{
    [Header("연결 설정")]
    [SerializeField] private string robotIP = "192.168.0.3";
    [SerializeField] private int robotPort = 3021;  // JSON-RPC 포트 (실제 로봇)

    [Header("UI - 연결")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("UI - 관절 슬라이더 (J1~J6)")]
    [SerializeField] private Slider sliderJ1;
    [SerializeField] private Slider sliderJ2;
    [SerializeField] private Slider sliderJ3;
    [SerializeField] private Slider sliderJ4;
    [SerializeField] private Slider sliderJ5;
    [SerializeField] private Slider sliderJ6;

    [Header("UI - 각도 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI textJ1;
    [SerializeField] private TextMeshProUGUI textJ2;
    [SerializeField] private TextMeshProUGUI textJ3;
    [SerializeField] private TextMeshProUGUI textJ4;
    [SerializeField] private TextMeshProUGUI textJ5;
    [SerializeField] private TextMeshProUGUI textJ6;

    [Header("UI - 제어 버튼")]
    [SerializeField] private Button sendButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button getCurrentPosButton;
    [SerializeField] private Button allResetButton;
    [SerializeField] private Button powerOffButton;

    [Header("UI - 로딩")]
    [SerializeField] private GameObject loadingIndicator;

    [Header("UI - 테스트 패널")]
    [SerializeField] private Button togglePanelButton;
    [SerializeField] private GameObject controlPanel;

    [Header("UI - 그리퍼")]
    [SerializeField] private Slider gripperPositionSlider;
    [SerializeField] private Slider gripperForceSlider;
    [SerializeField] private TextMeshProUGUI gripperPositionText;
    [SerializeField] private TextMeshProUGUI gripperForceText;

    [Header("UI - 속도/가속도")]
    [SerializeField] private Slider velocitySlider;
    [SerializeField] private Slider accelerationSlider;
    [SerializeField] private TextMeshProUGUI velocityText;
    [SerializeField] private TextMeshProUGUI accelerationText;

    private const float JOINT_LIMIT_DEG = 175f;

    private float velocity = 0.5f;
    private float acceleration = 1.0f;

    private bool isConnected = false;
    private string baseUrl;
    private int requestId = 1;

    private Slider[] sliders;
    private TextMeshProUGUI[] angleTexts;

    private string logFilePath;

    // 스로틀링 - 그리퍼 명령 전송 빈도 제한
    private float lastGripperCommandTime = 0f;
    private const float GRIPPER_COMMAND_INTERVAL = 0.1f; // 100ms 간격으로 제한
    private bool gripperCommandPending = false;

    void Start()
    {
        // 빌드 파일 옆에 로그 생성 (에디터에서는 Assets 폴더 옆)
        string appPath = Application.dataPath;
        string logDir = Application.isEditor
            ? Directory.GetParent(appPath).FullName  // 에디터: 프로젝트 루트
            : Directory.GetParent(appPath).FullName; // 빌드: exe 파일 위치

        logFilePath = Path.Combine(logDir, "LebaiRobotLog.txt");

        // 기존 로그 파일 삭제 후 새로 생성
        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        WriteLog("=== Lebai Robot Controller 시작 (HTTP JSON-RPC) ===");

        sliders = new Slider[] { sliderJ1, sliderJ2, sliderJ3, sliderJ4, sliderJ5, sliderJ6 };
        angleTexts = new TextMeshProUGUI[] { textJ1, textJ2, textJ3, textJ4, textJ5, textJ6 };

        foreach (var slider in sliders)
        {
            if (slider != null)
            {
                slider.minValue = -JOINT_LIMIT_DEG;
                slider.maxValue = JOINT_LIMIT_DEG;
                slider.value = 0;
            }
        }

        for (int i = 0; i < sliders.Length; i++)
        {
            int index = i;
            if (sliders[i] != null)
            {
                sliders[i].onValueChanged.AddListener((value) => OnSliderChanged(index, value));
            }
        }

        if (connectButton != null) connectButton.onClick.AddListener(Connect);
        if (disconnectButton != null) disconnectButton.onClick.AddListener(Disconnect);
        if (sendButton != null) sendButton.onClick.AddListener(SendJointMove);
        if (stopButton != null) stopButton.onClick.AddListener(StopRobot);
        if (getCurrentPosButton != null) getCurrentPosButton.onClick.AddListener(GetCurrentJointPositions);
        if (allResetButton != null) allResetButton.onClick.AddListener(AllResetSequential);
        if (togglePanelButton != null) togglePanelButton.onClick.AddListener(ToggleControlPanel);
        if (powerOffButton != null) powerOffButton.onClick.AddListener(PowerOff);


        if (gripperPositionSlider != null)
        {
            gripperPositionSlider.minValue = 0;
            gripperPositionSlider.maxValue = 100;
            gripperPositionSlider.value = 0; // 그리퍼 초기 상태는 닫힘 (amplitude=0)
            gripperPositionSlider.onValueChanged.AddListener(OnGripperPositionChanged);
        }
        if (gripperForceSlider != null)
        {
            gripperForceSlider.minValue = 0;
            gripperForceSlider.maxValue = 100;
            gripperForceSlider.value = 50;
            gripperForceSlider.onValueChanged.AddListener(OnGripperForceChanged);
        }

        if (velocitySlider != null)
        {
            velocitySlider.minValue = 0.1f;
            velocitySlider.maxValue = 3.0f;
            velocitySlider.value = velocity;
            velocitySlider.onValueChanged.AddListener(OnVelocityChanged);
        }
        if (accelerationSlider != null)
        {
            accelerationSlider.minValue = 0.1f;
            accelerationSlider.maxValue = 5.0f;
            accelerationSlider.value = acceleration;
            accelerationSlider.onValueChanged.AddListener(OnAccelerationChanged);
        }

        UpdateStatus("연결 안됨");
        UpdateAllAngleTexts();
        UpdateGripperTexts();
        UpdateVelocityAccelerationTexts();
    }

    void OnSliderChanged(int jointIndex, float value)
    {
        if (angleTexts[jointIndex] != null)
        {
            angleTexts[jointIndex].text = $"J{jointIndex + 1}: {value:F1}°";
        }
    }

    void OnGripperPositionChanged(float value)
    {
        if (gripperPositionText != null)
        {
            gripperPositionText.text = $"열림: {value:F0}%";
        }
        // 그리퍼 명령 전송
        SendGripperCommand();
    }

    void OnGripperForceChanged(float value)
    {
        if (gripperForceText != null)
        {
            gripperForceText.text = $"힘: {value:F0}%";
        }
        // 그리퍼 명령 전송
        SendGripperCommand();
    }

    private void SendGripperCommand()
    {
        if (!isConnected) return;
        if (gripperPositionSlider == null || gripperForceSlider == null) return;

        // 스로틀링: 마지막 명령 이후 일정 시간이 지났는지 확인
        if (Time.time - lastGripperCommandTime < GRIPPER_COMMAND_INTERVAL)
        {
            gripperCommandPending = true;
            return;
        }

        SendGripperCommandImmediate();
    }

    private void SendGripperCommandImmediate()
    {
        lastGripperCommandTime = Time.time;
        gripperCommandPending = false;

        // amplitude: 0~100 (0=완전히 닫힘, 100=완전히 열림)
        // force: 0~100 (그리퍼 힘)
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string amplitude = gripperPositionSlider.value.ToString("F1", culture);
        string force = gripperForceSlider.value.ToString("F1", culture);

        string paramsJson = $"[{{\"amplitude\": {amplitude}, \"force\": {force}}}]";
        WriteLog($"[GRIPPER] set_claw 전송: {paramsJson}");

        // Fire-and-forget: 응답을 기다리지 않고 바로 전송
        // 이렇게 하면 슬라이더를 빠르게 움직여도 끊김 없이 부드럽게 움직임
        _ = SendJsonRpcFireAndForget("set_claw", paramsJson);
    }

    /// <summary>
    /// Fire-and-forget 방식의 JSON-RPC 전송 (응답 대기 없음)
    /// 그리퍼처럼 빠른 연속 명령이 필요한 경우 사용
    /// </summary>
    private async Task SendJsonRpcFireAndForget(string method, string paramsJson)
    {
        try
        {
            await SendJsonRpc(method, paramsJson);
        }
        catch (Exception e)
        {
            WriteLog($"[GRIPPER] Fire-and-forget 에러: {e.Message}");
        }
    }

    void Update()
    {
        // 스로틀링된 그리퍼 명령이 대기 중이면 전송
        if (gripperCommandPending && Time.time - lastGripperCommandTime >= GRIPPER_COMMAND_INTERVAL)
        {
            SendGripperCommandImmediate();
        }
    }

    void UpdateGripperTexts()
    {
        if (gripperPositionSlider != null && gripperPositionText != null)
        {
            gripperPositionText.text = $"열림: {gripperPositionSlider.value:F0}%";
        }
        if (gripperForceSlider != null && gripperForceText != null)
        {
            gripperForceText.text = $"힘: {gripperForceSlider.value:F0}%";
        }
    }

    void OnVelocityChanged(float value)
    {
        velocity = value;
        if (velocityText != null)
        {
            velocityText.text = $"속도: {value:F1} rad/s";
        }
    }

    void OnAccelerationChanged(float value)
    {
        acceleration = value;
        if (accelerationText != null)
        {
            accelerationText.text = $"가속도: {value:F1} rad/s²";
        }
    }

    void UpdateVelocityAccelerationTexts()
    {
        if (velocitySlider != null && velocityText != null)
        {
            velocityText.text = $"속도: {velocitySlider.value:F1} rad/s";
        }
        if (accelerationSlider != null && accelerationText != null)
        {
            accelerationText.text = $"가속도: {accelerationSlider.value:F1} rad/s²";
        }
    }

    void UpdateAllAngleTexts()
    {
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] != null && angleTexts[i] != null)
            {
                angleTexts[i].text = $"J{i + 1}: {sliders[i].value:F1}°";
            }
        }
    }

    public async void Connect()
    {
        string ip = robotIP;
        if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
        {
            ip = ipInputField.text;
        }

        int port = robotPort;
        if (portInputField != null && !string.IsNullOrWhiteSpace(portInputField.text))
        {
            if (int.TryParse(portInputField.text, out int parsedPort))
            {
                port = parsedPort;
            }
        }

        baseUrl = $"http://{ip}:{port}";
        WriteLog($"[CONNECT] 연결 시도: {baseUrl}");
        UpdateStatus("연결 확인 중...");

        // 로봇 상태 확인으로 연결 테스트
        string response = await SendJsonRpc("get_robot_state", "[{}]");

        if (response != null && response.Contains("result"))
        {
            isConnected = true;
            WriteLog($"[CONNECT] 연결 성공: {baseUrl}");
            UpdateStatus($"연결됨: {ip}:{port}");

            // 로봇 자동 시작 (L Master에서 '시작' 버튼 누르는 것과 동일)
            string startResponse = await SendJsonRpc("start_sys", "[{}]");
            if (startResponse != null && startResponse.Contains("result"))
            {
                WriteLog("[CONNECT] 로봇 자동 시작 완료");
            }
            else
            {
                WriteLog($"[CONNECT] 로봇 시작 실패 (수동으로 시작 필요): {startResponse}");
            }

            // 그리퍼 초기화 (열기/닫기 1회로 스트로크 확인)
            string clawResponse = await SendJsonRpc("init_claw", "[{}]");
            if (clawResponse != null && clawResponse.Contains("result"))
            {
                WriteLog("[CONNECT] 그리퍼 초기화 완료");
            }
            else
            {
                WriteLog($"[CONNECT] 그리퍼 초기화 실패: {clawResponse}");
            }

            // 연결 성공 후 현재 로봇 위치를 UI에 동기화
            GetCurrentJointPositions();
        }
        else
        {
            isConnected = false;
            WriteLog($"[CONNECT] 연결 실패: {response}");
            UpdateStatus($"연결 실패 - 로봇 응답 없음");
        }
    }

    public async void Disconnect()
    {
        if (isConnected)
        {
            // 로봇 정지 (L Master에서 '정지' 버튼 누르는 것과 동일)
            string stopResponse = await SendJsonRpc("stop_sys", "[{}]");
            if (stopResponse != null && stopResponse.Contains("result"))
            {
                WriteLog("[DISCONNECT] 로봇 정지 완료");
            }
            else
            {
                WriteLog($"[DISCONNECT] 로봇 정지 실패: {stopResponse}");
            }
        }

        isConnected = false;
        UpdateStatus("연결 해제됨");
        WriteLog("[DISCONNECT] 연결 해제");
    }

    public async void PowerOff()
    {
        if (!isConnected)
        {
            WriteLog("[POWER] 연결되지 않음");
            return;
        }

        WriteLog("[POWER] 전원 끄기 시도...");
        string response = await SendJsonRpc("powerdown", "[{}]");
        if (response != null && response.Contains("result"))
        {
            WriteLog("[POWER] 전원 끄기 명령 전송 완료");
            UpdateStatus("전원 꺼짐");
        }
        else
        {
            WriteLog($"[POWER] 전원 끄기 실패: {response}");
        }

        isConnected = false;
    }

    public void ToggleControlPanel()
    {
        if (controlPanel != null)
        {
            controlPanel.SetActive(!controlPanel.activeSelf);
        }
    }

    /// <summary>
    /// 모든 축을 순차적으로 0도로 리셋 (충돌 방지)
    /// 안전 순서:
    /// 1단계: J5, J6 먼저 0°로 (손목 정리)
    /// 2단계: J4 → 0° (팔꿈치 방향 정리)
    /// 3단계: J3 → -90° (팔을 위로 펴기 - 안전 자세)
    /// 4단계: J2 → 0° (어깨 정리)
    /// 5단계: J1 → 0° (베이스 회전)
    /// 6단계: J3 → 0° (최종 자세)
    /// </summary>
    public async void AllResetSequential()
    {
        if (!isConnected)
        {
            UpdateStatus("로봇에 연결되지 않음");
            return;
        }

        // 로딩 표시 활성화
        if (loadingIndicator != null) loadingIndicator.SetActive(true);

        WriteLog("[ALL_RESET] 안전 순차 리셋 시작");
        UpdateStatus("안전 리셋 시작...");

        // 현재 각도 저장
        double[] currentAnglesRad = new double[6];
        for (int i = 0; i < 6; i++)
        {
            currentAnglesRad[i] = sliders[i] != null ? sliders[i].value * Mathf.Deg2Rad : 0;
        }

        // 안전 리셋 순서 (인덱스, 목표각도)
        // J3는 먼저 -90°로 가서 팔을 위로 펴고, 마지막에 0°로
        (int joint, float targetDeg)[] safeResetOrder = {
            (5, 0),      // J6 → 0°
            (4, 0),      // J5 → 0°
            (3, 0),      // J4 → 0°
            (2, -90),    // J3 → -90° (팔을 위로 - 안전 자세)
            (1, 0),      // J2 → 0°
            (0, 0),      // J1 → 0°
            (2, 0),      // J3 → 0° (최종)
        };

        foreach (var (jointIndex, targetDeg) in safeResetOrder)
        {
            float targetRad = targetDeg * Mathf.Deg2Rad;

            // 이미 목표 각도에 가까우면 스킵
            if (Mathf.Abs((float)currentAnglesRad[jointIndex] - targetRad) < 0.01f)
            {
                WriteLog($"[ALL_RESET] J{jointIndex + 1} 이미 {targetDeg}° - 스킵");
                continue;
            }

            UpdateStatus($"J{jointIndex + 1} → {targetDeg}°...");
            WriteLog($"[ALL_RESET] J{jointIndex + 1} → {targetDeg}°");

            currentAnglesRad[jointIndex] = targetRad;
            await SendMoveCommand(currentAnglesRad);

            // 로봇이 IDLE 상태가 될 때까지 대기
            await WaitUntilRobotIdle();
        }

        // 모든 리셋 완료 후 실제 로봇 위치를 읽어서 UI에 반영 (완료될 때까지 대기)
        WriteLog("[ALL_RESET] 안전 리셋 완료 - 위치 동기화 중...");
        await GetCurrentJointPositionsAsync();

        // 로딩 표시 비활성화 (UI 반영 완료 후)
        if (loadingIndicator != null) loadingIndicator.SetActive(false);

        WriteLog("[ALL_RESET] 안전 리셋 완료");
        UpdateStatus("안전 리셋 완료 - 모든 축 0°");
    }

    /// <summary>
    /// 지정된 각도로 이동 명령 전송
    /// </summary>
    private async Task SendMoveCommand(double[] anglesRad)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string jointArray = string.Format(culture, "[{0:F6}, {1:F6}, {2:F6}, {3:F6}, {4:F6}, {5:F6}]",
            anglesRad[0], anglesRad[1], anglesRad[2], anglesRad[3], anglesRad[4], anglesRad[5]);

        string vStr = velocity.ToString("F2", culture);
        string aStr = acceleration.ToString("F2", culture);

        string paramsJson = $"[{{\"pose\": {{\"kind\": 1, \"joint\": {{\"joint\": {jointArray}}}}}, \"param\": {{\"velocity\": {vStr}, \"acc\": {aStr}}}}}]";

        WriteLog($"[MOVE] move_joint 전송: {paramsJson}");
        await SendJsonRpc("move_joint", paramsJson);
    }

    public async void SendJointMove()
    {
        if (!isConnected)
        {
            UpdateStatus("로봇에 연결되지 않음");
            return;
        }

        // 각도를 라디안으로 변환
        double[] anglesRad = new double[6];
        for (int i = 0; i < 6; i++)
        {
            anglesRad[i] = sliders[i] != null ? sliders[i].value * Mathf.Deg2Rad : 0;
        }

        // JSON-RPC move_joint 호출
        // Lebai Proto 문서 기반 올바른 형식:
        // pose: { kind: 1 (JOINT), joint: { joint: [j1,j2,j3,j4,j5,j6] } }
        // param: { velocity: v, acc: a }
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string jointArray = string.Format(culture, "[{0:F6}, {1:F6}, {2:F6}, {3:F6}, {4:F6}, {5:F6}]",
            anglesRad[0], anglesRad[1], anglesRad[2], anglesRad[3], anglesRad[4], anglesRad[5]);

        string vStr = velocity.ToString("F2", culture);
        string aStr = acceleration.ToString("F2", culture);

        // MoveRequest 형식: { pose: Pose, param: MoveParam }
        // Pose: { kind: 1 (JOINT), joint: JointPose }
        // JointPose: { joint: [double array] }
        // MoveParam: { velocity: double, acc: double }
        string paramsJson = $"[{{\"pose\": {{\"kind\": 1, \"joint\": {{\"joint\": {jointArray}}}}}, \"param\": {{\"velocity\": {vStr}, \"acc\": {aStr}}}}}]";

        WriteLog($"[MOVE] move_joint 전송: {paramsJson}");
        UpdateStatus("이동 명령 전송 중...");

        // move_joint 메소드 사용 (관절 공간 이동)
        string response = await SendJsonRpc("move_joint", paramsJson);

        if (response != null)
        {
            if (response.Contains("error"))
            {
                WriteLog($"[MOVE] 에러: {response}");
                UpdateStatus($"이동 실패");
            }
            else
            {
                WriteLog($"[MOVE] 성공: {response}");
                UpdateStatus($"이동 명령 전송 완료");
            }
        }
        else
        {
            WriteLog("[MOVE] 응답 없음");
            UpdateStatus("이동 명령 실패 - 응답 없음");
        }
    }

    public async void StopRobot()
    {
        if (!isConnected) return;

        WriteLog("[STOP] stop_move 전송");
        string response = await SendJsonRpc("stop_move", "[{}]");
        WriteLog($"[STOP] 응답: {response}");
        UpdateStatus("정지 명령 전송");
    }

    /// <summary>
    /// 로봇이 IDLE 상태가 될 때까지 대기
    /// </summary>
    private async Task WaitUntilRobotIdle(int maxWaitMs = 30000)
    {
        WriteLog("[WAIT] 로봇 정지 대기 시작...");
        int waited = 0;
        int pollInterval = 200; // 200ms마다 체크

        while (waited < maxWaitMs)
        {
            string response = await SendJsonRpc("get_robot_state", "[{}]");
            if (response != null && response.Contains("\"IDLE\""))
            {
                WriteLog("[WAIT] 로봇 IDLE 상태 확인");
                return;
            }

            await Task.Delay(pollInterval);
            waited += pollInterval;
        }

        WriteLog($"[WAIT] 타임아웃 ({maxWaitMs}ms) - 강제 진행");
    }

    public async void GetCurrentJointPositions()
    {
        await GetCurrentJointPositionsAsync();
    }

    private async Task GetCurrentJointPositionsAsync()
    {
        if (!isConnected)
        {
            UpdateStatus("로봇에 연결되지 않음");
            return;
        }

        WriteLog("[GET_POS] get_kin_data 요청");

        string response = await SendJsonRpc("get_kin_data", "[{}]");

        if (response != null)
        {
            WriteLog($"[GET_POS] 응답: {response}");
            ParseJsonRpcJointPositions(response);
        }
        else
        {
            WriteLog("[GET_POS] 응답 없음");
            UpdateStatus("위치 가져오기 실패");
        }
    }

    private void ParseJsonRpcJointPositions(string response)
    {
        try
        {
            // get_kin_data 응답: {"jsonrpc":"2.0","result":{"actual_joint_pose":[j1,j2,j3,j4,j5,j6],...},"id":1}

            // actual_joint_pose 찾기
            string searchKey = "\"actual_joint_pose\"";
            int keyIdx = response.IndexOf(searchKey);
            if (keyIdx < 0)
            {
                // 다른 형식 시도: 바로 배열인 경우
                keyIdx = response.IndexOf("\"result\"");
                if (keyIdx < 0)
                {
                    WriteLog("[GET_POS] actual_joint_pose/result 필드 없음");
                    return;
                }
            }

            // 배열 시작 찾기
            int arrayStart = response.IndexOf('[', keyIdx);
            if (arrayStart < 0) return;

            int arrayEnd = response.IndexOf(']', arrayStart);
            if (arrayEnd < 0) return;

            string arrayStr = response.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            string[] values = arrayStr.Split(',');

            if (values.Length >= 6)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (double.TryParse(values[i].Trim(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double radValue))
                    {
                        float degValue = (float)(radValue * Mathf.Rad2Deg);
                        degValue = Mathf.Clamp(degValue, -JOINT_LIMIT_DEG, JOINT_LIMIT_DEG);

                        if (sliders[i] != null)
                        {
                            sliders[i].value = degValue;
                        }
                        WriteLog($"[GET_POS] J{i + 1}: {radValue:F4} rad = {degValue:F1}°");
                    }
                }
                UpdateStatus("현재 위치 동기화 완료");
            }
            else
            {
                WriteLog($"[GET_POS] 파싱 실패 - 값 개수: {values.Length}");
            }
        }
        catch (Exception e)
        {
            WriteLog($"[GET_POS] 파싱 오류: {e.Message}");
        }
    }

    private static readonly HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };

    private async Task<string> SendJsonRpc(string method, string paramsJson)
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            WriteLog("[ERROR] baseUrl이 설정되지 않음");
            return null;
        }

        string jsonBody = $"{{\"jsonrpc\":\"2.0\",\"method\":\"{method}\",\"params\":{paramsJson},\"id\":{requestId++}}}";

        WriteLog($"[JSON-RPC] 요청: {jsonBody}");

        try
        {
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PostAsync(baseUrl, content);

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                WriteLog($"[JSON-RPC] 응답: {responseBody}");
                return responseBody;
            }
            else
            {
                WriteLog($"[JSON-RPC] HTTP 오류: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception e)
        {
            WriteLog($"[JSON-RPC] 예외: {e.Message}");
            return null;
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[Lebai] {message}");
        WriteLog($"[STATUS] {message}");
    }

    private void WriteLog(string message)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] {message}\n";
            File.AppendAllText(logFilePath, logMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"로그 파일 쓰기 실패: {e.Message}");
        }
    }

    void OnDestroy()
    {
        WriteLog("=== Lebai Robot Controller 종료 ===");
    }
}
