using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#region 티칭 시스템 데이터 클래스
/// <summary>
/// 티칭 JSON 파일의 전체 구조
/// </summary>
[Serializable]
public class TeachingData
{
    public string name;           // 티칭 이름 (예: "퍼즐 게임 180초")
    public string description;    // 설명
    public float totalDuration;   // 전체 소요 시간 (초)
    public List<TeachingStep> steps;  // 티칭 스텝 목록
}

/// <summary>
/// 티칭의 각 스텝 (동작 하나)
/// </summary>
[Serializable]
public class TeachingStep
{
    public int stepNumber;        // 스텝 번호 (1부터 시작)
    public string name;           // 스텝 이름 (예: "퍼즐 집기")
    public float time;            // 시작 시간 (초) - 티칭 시작 후 몇 초에 시작
    public float duration;        // 동작 소요 시간 (초) - t 파라미터로 사용
    public TeachingAction action; // 실행할 동작
}

/// <summary>
/// 실행할 동작 정의
/// </summary>
[Serializable]
public class TeachingAction
{
    public string type;           // "move_joint", "set_gripper", "set_do", "wait"
    public float[] joints;        // move_joint용: [J1, J2, J3, J4, J5, J6] (도 단위)
    public float gripperPosition = -1f; // set_gripper용: 그리퍼 위치 (0~100), -1이면 미설정
    public float gripperForce = -1f;    // set_gripper용: 그리퍼 힘 (0~100), -1이면 미설정
    public float velocity = -1f;        // 선택적: 속도 오버라이드, -1이면 기본값 사용
    public float acceleration = -1f;    // 선택적: 가속도 오버라이드, -1이면 기본값 사용

    // set_do용 (버큠 그리퍼/디지털 출력 제어)
    public string device = "FLANGE";    // "FLANGE", "ROBOT", "EXTRA"
    public int pin = 0;                 // 핀 번호 (0부터 시작)
    public int value = 0;               // 0=OFF, 1=ON
}
#endregion

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

    [Header("UI - 이동 단위 설정")]
    [SerializeField] private TMP_InputField jointSettingInput;  // 관절 이동 단위 (예: 0.1도)
    [SerializeField] private TMP_InputField velocityAccelSettingInput;  // 속도/가속도 이동 단위
    [SerializeField] private TMP_InputField gripperSettingInput;  // 그리퍼 이동 단위
    private float jointSettingValue = 0.1f;  // 기본값 0.1도
    private float velocityAccelSettingValue = 0.1f;  // 기본값 0.1
    private float gripperSettingValue = 5.0f;  // 기본값 5%

    [Header("UI - 각도 입력 필드 (J1~J6)")]
    [SerializeField] private TMP_InputField inputJ1;
    [SerializeField] private TMP_InputField inputJ2;
    [SerializeField] private TMP_InputField inputJ3;
    [SerializeField] private TMP_InputField inputJ4;
    [SerializeField] private TMP_InputField inputJ5;
    [SerializeField] private TMP_InputField inputJ6;

    [Header("UI - 좌/우 버튼 (J1~J6)")]
    [SerializeField] private Button btnJ1Left;
    [SerializeField] private Button btnJ1Right;
    [SerializeField] private Button btnJ2Left;
    [SerializeField] private Button btnJ2Right;
    [SerializeField] private Button btnJ3Left;
    [SerializeField] private Button btnJ3Right;
    [SerializeField] private Button btnJ4Left;
    [SerializeField] private Button btnJ4Right;
    [SerializeField] private Button btnJ5Left;
    [SerializeField] private Button btnJ5Right;
    [SerializeField] private Button btnJ6Left;
    [SerializeField] private Button btnJ6Right;

    [Header("UI - 제어 버튼")]
    [SerializeField] private Button stopButton;
    [SerializeField] private Button getCurrentPosButton;
    [SerializeField] private Button allResetButton;
    [SerializeField] private Button powerOffButton;

    [Header("UI - 로딩")]
    [SerializeField] private GameObject loadingIndicator;  // Init/일반 동작용 로딩
    [SerializeField] private GameObject connectLoadingIndicator;  // Connect용 로딩

    [Header("UI - 테스트 패널")]
    [SerializeField] private Button togglePanelButton;
    [SerializeField] private GameObject controlPanel;

    [Header("UI - 그리퍼")]
    [SerializeField] private Slider gripperPositionSlider;
    [SerializeField] private Slider gripperForceSlider;
    [SerializeField] private TMP_InputField gripperPositionInput;
    [SerializeField] private TMP_InputField gripperForceInput;
    [SerializeField] private Button btnGripperPosLeft;
    [SerializeField] private Button btnGripperPosRight;
    [SerializeField] private Button btnGripperForceLeft;
    [SerializeField] private Button btnGripperForceRight;

    [Header("UI - 속도/가속도")]
    [SerializeField] private Slider velocitySlider;
    [SerializeField] private Slider accelerationSlider;
    [SerializeField] private TMP_InputField velocityInput;
    [SerializeField] private TMP_InputField accelerationInput;
    [SerializeField] private Button btnVelocityLeft;
    [SerializeField] private Button btnVelocityRight;
    [SerializeField] private Button btnAccelLeft;
    [SerializeField] private Button btnAccelRight;

    [Header("UI - 티칭 시스템")]
    [SerializeField] private Button teachingButton;  // 티칭 시작 버튼
    [SerializeField] private Button stopTeachingButton;  // 티칭 중지 버튼
    [SerializeField] private TextMeshProUGUI teachingStatusText;  // 티칭 상태 표시
    [SerializeField] private string teachingFileName = "robot_teaching.json";  // JSON 파일 이름
    [SerializeField] private GameObject teachingActiveIndicator;  // 티칭 중 활성화되는 오브젝트

    [Header("홈 포지션 설정")]
    [SerializeField] private float[] homePosition = new float[] { 0f, 0f, 0f, 0f, 0f, 0f };  // 홈 포지션 (도 단위)
    [SerializeField] private float homeMoveTime = 5f;  // 홈 포지션 이동 시간 (초)
    [SerializeField] private bool autoHomeOnConnect = true;  // 연결 시 자동으로 홈 포지션 이동

    private const float JOINT_LIMIT_DEG = 175f;

    private float velocity = 0.5f;
    private float acceleration = 1.0f;

    private bool isConnected = false;
    private string baseUrl;
    private int requestId = 1;

    private Slider[] sliders;
    private TMP_InputField[] angleInputs;

    private string logFilePath;

    // 스로틀링 - 그리퍼 명령 전송 빈도 제한
    private float lastGripperCommandTime = 0f;
    private const float GRIPPER_COMMAND_INTERVAL = 0.1f; // 100ms 간격으로 제한
    private bool gripperCommandPending = false;

    // 위치 동기화 상태 (Init 후 Connect 시 불필요한 재동기화 방지)
    private bool positionSynced = false;

    // 티칭 시스템 상태
    private bool isTeachingRunning = false;
    private CancellationTokenSource teachingCancellation;
    private TeachingData currentTeachingData;
    private float teachingStartTime;

    // 홈 포지션 이동 상태 (이동 중 슬라이더 이벤트 무시)
    private bool isMovingToHome = false;

    // 동작 중 상태 (모든 입력 차단)
    private bool isBusy = false;

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
        angleInputs = new TMP_InputField[] { inputJ1, inputJ2, inputJ3, inputJ4, inputJ5, inputJ6 };

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
        if (stopButton != null) stopButton.onClick.AddListener(StopRobot);
        if (getCurrentPosButton != null) getCurrentPosButton.onClick.AddListener(GetCurrentJointPositions);
        if (allResetButton != null) allResetButton.onClick.AddListener(AllResetSequential);
        if (togglePanelButton != null) togglePanelButton.onClick.AddListener(ToggleControlPanel);
        if (powerOffButton != null) powerOffButton.onClick.AddListener(PowerOff);

        // 이동 단위 InputField 초기화
        if (jointSettingInput != null)
        {
            jointSettingInput.text = $"{jointSettingValue:F2}";
            jointSettingInput.onEndEdit.AddListener(OnJointSettingChanged);
        }
        if (velocityAccelSettingInput != null)
        {
            velocityAccelSettingInput.text = $"{velocityAccelSettingValue:F2}";
            velocityAccelSettingInput.onEndEdit.AddListener(OnVelocityAccelSettingChanged);
        }
        if (gripperSettingInput != null)
        {
            gripperSettingInput.text = $"{gripperSettingValue:F2}";
            gripperSettingInput.onEndEdit.AddListener(OnGripperSettingChanged);
        }

        // J1~J6 좌/우 버튼 연결
        Button[] leftButtons = { btnJ1Left, btnJ2Left, btnJ3Left, btnJ4Left, btnJ5Left, btnJ6Left };
        Button[] rightButtons = { btnJ1Right, btnJ2Right, btnJ3Right, btnJ4Right, btnJ5Right, btnJ6Right };
        for (int i = 0; i < 6; i++)
        {
            int index = i;
            if (leftButtons[i] != null) leftButtons[i].onClick.AddListener(() => MoveJoint(index, -1));
            if (rightButtons[i] != null) rightButtons[i].onClick.AddListener(() => MoveJoint(index, 1));
        }

        // J1~J6 각도 InputField Enter 이벤트 연결
        for (int i = 0; i < angleInputs.Length; i++)
        {
            int index = i;
            if (angleInputs[i] != null)
            {
                angleInputs[i].onEndEdit.AddListener((value) => OnAngleInputChanged(index, value));
            }
        }

        // 속도/가속도 InputField Enter 이벤트 연결
        if (velocityInput != null) velocityInput.onEndEdit.AddListener(OnVelocityInputChanged);
        if (accelerationInput != null) accelerationInput.onEndEdit.AddListener(OnAccelerationInputChanged);

        // 그리퍼 InputField Enter 이벤트 연결
        if (gripperPositionInput != null) gripperPositionInput.onEndEdit.AddListener(OnGripperPositionInputChanged);
        if (gripperForceInput != null) gripperForceInput.onEndEdit.AddListener(OnGripperForceInputChanged);

        // 속도/가속도 좌우 버튼 연결
        if (btnVelocityLeft != null) btnVelocityLeft.onClick.AddListener(() => MoveVelocity(-1));
        if (btnVelocityRight != null) btnVelocityRight.onClick.AddListener(() => MoveVelocity(1));
        if (btnAccelLeft != null) btnAccelLeft.onClick.AddListener(() => MoveAcceleration(-1));
        if (btnAccelRight != null) btnAccelRight.onClick.AddListener(() => MoveAcceleration(1));

        // 그리퍼 좌우 버튼 연결
        if (btnGripperPosLeft != null)
        {
            btnGripperPosLeft.onClick.AddListener(() => MoveGripperPosition(-1));
            WriteLog($"[INIT] btnGripperPosLeft 연결됨: {btnGripperPosLeft.gameObject.name}");
        }
        else
        {
            WriteLog("[INIT] WARNING: btnGripperPosLeft가 null입니다!");
        }
        if (btnGripperPosRight != null)
        {
            btnGripperPosRight.onClick.AddListener(() => MoveGripperPosition(1));
            WriteLog($"[INIT] btnGripperPosRight 연결됨: {btnGripperPosRight.gameObject.name}");
        }
        else
        {
            WriteLog("[INIT] WARNING: btnGripperPosRight가 null입니다!");
        }
        if (btnGripperForceLeft != null)
        {
            btnGripperForceLeft.onClick.AddListener(() => MoveGripperForce(-1));
            WriteLog($"[INIT] btnGripperForceLeft 연결됨: {btnGripperForceLeft.gameObject.name}");
        }
        else
        {
            WriteLog("[INIT] WARNING: btnGripperForceLeft가 null입니다!");
        }
        if (btnGripperForceRight != null)
        {
            btnGripperForceRight.onClick.AddListener(() => MoveGripperForce(1));
            WriteLog($"[INIT] btnGripperForceRight 연결됨: {btnGripperForceRight.gameObject.name}");
        }
        else
        {
            WriteLog("[INIT] WARNING: btnGripperForceRight가 null입니다!");
        }


        if (gripperPositionSlider != null)
        {
            gripperPositionSlider.minValue = 0;
            gripperPositionSlider.maxValue = 100;
            gripperPositionSlider.value = 0; // 그리퍼 초기 상태는 닫힘 (amplitude=0)
            gripperPositionSlider.onValueChanged.AddListener(OnGripperPositionChanged);
            WriteLog($"[INIT] gripperPositionSlider 연결됨: {gripperPositionSlider.gameObject.name}");
        }
        else
        {
            WriteLog("[INIT] WARNING: gripperPositionSlider가 null입니다!");
        }
        if (gripperForceSlider != null)
        {
            gripperForceSlider.minValue = 0;
            gripperForceSlider.maxValue = 100;
            gripperForceSlider.value = 50;
            gripperForceSlider.onValueChanged.AddListener(OnGripperForceChanged);
            WriteLog($"[INIT] gripperForceSlider 연결됨: {gripperForceSlider.gameObject.name}");
        }
        else
        {
            WriteLog("[INIT] WARNING: gripperForceSlider가 null입니다!");
        }

        // 그리퍼 슬라이더가 같은 오브젝트인지 확인
        if (gripperPositionSlider != null && gripperForceSlider != null)
        {
            if (gripperPositionSlider == gripperForceSlider)
            {
                WriteLog("[INIT] ERROR: gripperPositionSlider와 gripperForceSlider가 같은 슬라이더입니다! Inspector에서 확인 필요!");
                Debug.LogError("[Lebai] gripperPositionSlider와 gripperForceSlider가 같은 슬라이더로 연결되어 있습니다!");
            }
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

        // 티칭 버튼 연결
        if (teachingButton != null)
        {
            teachingButton.onClick.AddListener(StartTeaching);
            WriteLog("[INIT] teachingButton 연결됨");
        }
        if (stopTeachingButton != null)
        {
            stopTeachingButton.onClick.AddListener(StopTeaching);
            WriteLog("[INIT] stopTeachingButton 연결됨");
        }

        UpdateStatus("연결 안됨");
        UpdateAllAngleTexts();
        UpdateGripperTexts();
        UpdateVelocityAccelerationTexts();
        UpdateTeachingStatus("대기 중");

        // 티칭 UI 초기 상태: 비활성화 (controlPanel 상태는 유지)
        SetTeachingUIActive(false, affectControlPanel: false);
    }

    // 조인트 슬라이더 스로틀링
    private float lastSliderSendTime = 0f;
    private const float SLIDER_SEND_INTERVAL = 0.1f; // 100ms 간격으로 전송

    void OnSliderChanged(int jointIndex, float value)
    {
        if (angleInputs[jointIndex] != null)
        {
            angleInputs[jointIndex].text = $"{value:F2}";
        }

        // 동작 중, 티칭 중, 홈 포지션 이동 중에는 슬라이더 이벤트 무시
        if (isBusy || isTeachingRunning || isMovingToHome) return;

        // 슬라이더 변경 시 로봇에 자동 전송 (스로틀링 적용)
        if (isConnected && Time.time - lastSliderSendTime >= SLIDER_SEND_INTERVAL)
        {
            lastSliderSendTime = Time.time;
            SendJointMove();
        }
    }

    /// <summary>
    /// 관절 이동 단위 InputField 값 변경 시
    /// </summary>
    void OnJointSettingChanged(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float newStep))
        {
            jointSettingValue = Mathf.Clamp(newStep, 0.01f, 180f);  // 최대값 180도로 증가
            WriteLog($"[SETTING] 관절 이동 단위 변경: {jointSettingValue}°");
        }
        if (jointSettingInput != null)
        {
            jointSettingInput.text = $"{jointSettingValue:F2}";
        }
    }

    /// <summary>
    /// 속도/가속도 이동 단위 InputField 값 변경 시
    /// </summary>
    void OnVelocityAccelSettingChanged(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float newStep))
        {
            velocityAccelSettingValue = Mathf.Clamp(newStep, 0.01f, 5f);  // 최대값 5로 증가
            WriteLog($"[SETTING] 속도/가속도 이동 단위 변경: {velocityAccelSettingValue}");
        }
        if (velocityAccelSettingInput != null)
        {
            velocityAccelSettingInput.text = $"{velocityAccelSettingValue:F2}";
        }
    }

    /// <summary>
    /// 그리퍼 이동 단위 InputField 값 변경 시
    /// </summary>
    void OnGripperSettingChanged(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float newStep))
        {
            gripperSettingValue = Mathf.Clamp(newStep, 0.1f, 20f);
            WriteLog($"[SETTING] 그리퍼 이동 단위 변경: {gripperSettingValue}%");
        }
        if (gripperSettingInput != null)
        {
            gripperSettingInput.text = $"{gripperSettingValue:F2}";
        }
    }

    /// <summary>
    /// 좌/우 버튼으로 관절 이동 (direction: -1 = 좌, 1 = 우)
    /// </summary>
    void MoveJoint(int jointIndex, int direction)
    {
        if (isBusy) return;  // 동작 중에는 무시
        if (sliders[jointIndex] == null) return;

        float currentValue = sliders[jointIndex].value;
        float delta = jointSettingValue * direction;
        float newValue = currentValue + delta;
        newValue = Mathf.Clamp(newValue, -JOINT_LIMIT_DEG, JOINT_LIMIT_DEG);

        WriteLog($"[JOINT] J{jointIndex + 1} 버튼: 현재={currentValue:F2}, 이동량={delta:F2} (설정값={jointSettingValue:F2}), 새값={newValue:F2}");

        sliders[jointIndex].value = newValue;

        // 자동 Send
        SendJointMove();
    }

    /// <summary>
    /// 좌/우 버튼으로 속도 조절
    /// </summary>
    void MoveVelocity(int direction)
    {
        if (isBusy) return;  // 동작 중에는 무시
        if (velocitySlider == null) return;

        float newValue = velocitySlider.value + (velocityAccelSettingValue * direction);
        newValue = Mathf.Clamp(newValue, 0.1f, 3.0f);
        velocitySlider.value = newValue;
    }

    /// <summary>
    /// 좌/우 버튼으로 가속도 조절
    /// </summary>
    void MoveAcceleration(int direction)
    {
        if (isBusy) return;  // 동작 중에는 무시
        if (accelerationSlider == null) return;

        float newValue = accelerationSlider.value + (velocityAccelSettingValue * direction);
        newValue = Mathf.Clamp(newValue, 0.1f, 5.0f);
        accelerationSlider.value = newValue;
    }

    /// <summary>
    /// 좌/우 버튼으로 그리퍼 포지션 조절
    /// </summary>
    void MoveGripperPosition(int direction)
    {
        if (isBusy) return;  // 동작 중에는 무시
        if (gripperPositionSlider == null) return;

        float newValue = gripperPositionSlider.value + (gripperSettingValue * direction);
        newValue = Mathf.Clamp(newValue, 0f, 100f);
        gripperPositionSlider.value = newValue;

        // 슬라이더 onValueChanged 이벤트가 호출되어 자동으로 SendGripperCommand 실행됨
        WriteLog($"[GRIPPER] Position 버튼: {direction}, 새 값: {newValue}");
    }

    /// <summary>
    /// 좌/우 버튼으로 그리퍼 힘 조절
    /// </summary>
    void MoveGripperForce(int direction)
    {
        if (isBusy) return;  // 동작 중에는 무시
        if (gripperForceSlider == null) return;

        float newValue = gripperForceSlider.value + (gripperSettingValue * direction);
        newValue = Mathf.Clamp(newValue, 0f, 100f);
        gripperForceSlider.value = newValue;

        // 슬라이더 onValueChanged 이벤트가 호출되어 자동으로 SendGripperCommand 실행됨
        WriteLog($"[GRIPPER] Force 버튼: {direction}, 새 값: {newValue}");
    }

    /// <summary>
    /// 각도 InputField에서 직접 입력 후 Enter
    /// </summary>
    void OnAngleInputChanged(int jointIndex, string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float angle))
        {
            angle = Mathf.Clamp(angle, -JOINT_LIMIT_DEG, JOINT_LIMIT_DEG);
            if (sliders[jointIndex] != null)
            {
                sliders[jointIndex].value = angle;
            }
            // 자동 Send
            SendJointMove();
        }
        // InputField 값 다시 표시 (클램핑된 값 반영)
        if (angleInputs[jointIndex] != null && sliders[jointIndex] != null)
        {
            angleInputs[jointIndex].text = $"{sliders[jointIndex].value:F2}";
        }
    }

    /// <summary>
    /// 속도 InputField 입력
    /// </summary>
    void OnVelocityInputChanged(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float v))
        {
            v = Mathf.Clamp(v, 0.1f, 3.0f);
            velocity = v;
            if (velocitySlider != null) velocitySlider.value = v;
        }
        if (velocityInput != null)
        {
            velocityInput.text = $"{velocity:F2}";
        }
    }

    /// <summary>
    /// 가속도 InputField 입력
    /// </summary>
    void OnAccelerationInputChanged(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float a))
        {
            a = Mathf.Clamp(a, 0.1f, 5.0f);
            acceleration = a;
            if (accelerationSlider != null) accelerationSlider.value = a;
        }
        if (accelerationInput != null)
        {
            accelerationInput.text = $"{acceleration:F2}";
        }
    }

    /// <summary>
    /// 그리퍼 위치 InputField 입력
    /// </summary>
    void OnGripperPositionInputChanged(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float pos))
        {
            pos = Mathf.Clamp(pos, 0f, 100f);
            if (gripperPositionSlider != null) gripperPositionSlider.value = pos;
        }
        if (gripperPositionInput != null && gripperPositionSlider != null)
        {
            gripperPositionInput.text = $"{gripperPositionSlider.value:F0}";
        }
    }

    /// <summary>
    /// 그리퍼 힘 InputField 입력
    /// </summary>
    void OnGripperForceInputChanged(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float force))
        {
            force = Mathf.Clamp(force, 0f, 100f);
            if (gripperForceSlider != null) gripperForceSlider.value = force;
        }
        if (gripperForceInput != null && gripperForceSlider != null)
        {
            gripperForceInput.text = $"{gripperForceSlider.value:F0}";
        }
    }

    void OnGripperPositionChanged(float value)
    {
        if (gripperPositionInput != null)
        {
            gripperPositionInput.text = $"{value:F0}";
        }

        // 티칭 중 또는 홈 이동 중에는 이벤트 무시 (티칭에서 직접 제어)
        if (isTeachingRunning || isMovingToHome) return;

        // 그리퍼 명령 전송
        SendGripperCommand();
    }

    void OnGripperForceChanged(float value)
    {
        if (gripperForceInput != null)
        {
            gripperForceInput.text = $"{value:F0}";
        }

        // 티칭 중 또는 홈 이동 중에는 이벤트 무시 (티칭에서 직접 제어)
        if (isTeachingRunning || isMovingToHome) return;

        // 그리퍼 명령 전송
        SendGripperCommand();
    }

    private void SendGripperCommand()
    {
        if (!isConnected) return;
        if (isBusy) return;  // 동작 중에는 무시
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
        if (gripperPositionSlider != null && gripperPositionInput != null)
        {
            gripperPositionInput.text = $"{gripperPositionSlider.value:F0}";
        }
        if (gripperForceSlider != null && gripperForceInput != null)
        {
            gripperForceInput.text = $"{gripperForceSlider.value:F0}";
        }
    }

    void OnVelocityChanged(float value)
    {
        velocity = value;
        if (velocityInput != null)
        {
            velocityInput.text = $"{value:F2}";
        }
    }

    void OnAccelerationChanged(float value)
    {
        acceleration = value;
        if (accelerationInput != null)
        {
            accelerationInput.text = $"{value:F2}";
        }
    }

    void UpdateVelocityAccelerationTexts()
    {
        if (velocitySlider != null && velocityInput != null)
        {
            velocityInput.text = $"{velocitySlider.value:F2}";
        }
        if (accelerationSlider != null && accelerationInput != null)
        {
            accelerationInput.text = $"{accelerationSlider.value:F2}";
        }
    }

    void UpdateAllAngleTexts()
    {
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] != null && angleInputs[i] != null)
            {
                angleInputs[i].text = $"{sliders[i].value:F2}";
            }
        }
    }

    public async void Connect()
    {
        // 이미 동작 중이면 무시
        if (isBusy)
        {
            WriteLog("[CONNECT] 이미 동작 중 - 무시");
            return;
        }

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

        // Connect용 로딩 인디케이터 활성화
        if (connectLoadingIndicator != null) connectLoadingIndicator.SetActive(true);

        try
        {
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

                // 연결 성공 후 현재 로봇 위치를 UI에 동기화 (이미 동기화된 경우 스킵)
                if (!positionSynced)
                {
                    await GetCurrentJointPositionsAsync();
                }
                else
                {
                    WriteLog("[CONNECT] 이미 위치 동기화됨 - 재동기화 스킵");
                }

                // Connect 로딩 비활성화
                if (connectLoadingIndicator != null) connectLoadingIndicator.SetActive(false);

                // 홈 포지션으로 자동 이동 (설정된 경우)
                if (autoHomeOnConnect)
                {
                    await MoveToHomePosition();
                }
            }
            else
            {
                isConnected = false;
                WriteLog($"[CONNECT] 연결 실패: {response}");
                UpdateStatus($"연결 실패 - 로봇 응답 없음");
                if (connectLoadingIndicator != null) connectLoadingIndicator.SetActive(false);
            }
        }
        catch (Exception e)
        {
            WriteLog($"[CONNECT] 예외 발생: {e.Message}");
            UpdateStatus("연결 실패");
            if (connectLoadingIndicator != null) connectLoadingIndicator.SetActive(false);
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
    /// Init 버튼: 홈 포지션으로 이동
    /// </summary>
    public async void AllResetSequential()
    {
        // 홈 포지션으로 이동 (MoveToHomePosition이 isBusy 체크 및 UI 비활성화 처리)
        await MoveToHomePosition();
    }

    public async void SendJointMove()
    {
        if (!isConnected)
        {
            UpdateStatus("로봇에 연결되지 않음");
            return;
        }

        // 동작 중에는 무시
        if (isBusy)
        {
            WriteLog("[MOVE] 동작 중 - 무시");
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

    #region 동작 중 UI 제어

    /// <summary>
    /// 동작 중 상태 설정 및 UI 활성화/비활성화
    /// </summary>
    private void SetBusy(bool busy)
    {
        isBusy = busy;

        // 로딩 인디케이터
        if (loadingIndicator != null) loadingIndicator.SetActive(busy);

        // 버튼들 비활성화 (Stop 버튼은 항상 활성화)
        if (connectButton != null) connectButton.interactable = !busy;
        if (disconnectButton != null) disconnectButton.interactable = !busy;
        if (allResetButton != null) allResetButton.interactable = !busy;
        if (getCurrentPosButton != null) getCurrentPosButton.interactable = !busy;
        if (powerOffButton != null) powerOffButton.interactable = !busy;
        if (teachingButton != null) teachingButton.interactable = !busy;

        // 슬라이더들 비활성화
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] != null) sliders[i].interactable = !busy;
        }
        if (gripperPositionSlider != null) gripperPositionSlider.interactable = !busy;
        if (gripperForceSlider != null) gripperForceSlider.interactable = !busy;
        if (velocitySlider != null) velocitySlider.interactable = !busy;
        if (accelerationSlider != null) accelerationSlider.interactable = !busy;

        // 좌/우 버튼들 비활성화
        Button[] allButtons = {
            btnJ1Left, btnJ1Right, btnJ2Left, btnJ2Right, btnJ3Left, btnJ3Right,
            btnJ4Left, btnJ4Right, btnJ5Left, btnJ5Right, btnJ6Left, btnJ6Right,
            btnGripperPosLeft, btnGripperPosRight, btnGripperForceLeft, btnGripperForceRight,
            btnVelocityLeft, btnVelocityRight, btnAccelLeft, btnAccelRight
        };
        foreach (var btn in allButtons)
        {
            if (btn != null) btn.interactable = !busy;
        }
    }

    #endregion

    #region 홈 포지션

    /// <summary>
    /// 홈 포지션으로 이동 (Inspector에서 설정한 homePosition 값 사용)
    /// </summary>
    public async Task MoveToHomePosition()
    {
        if (!isConnected)
        {
            WriteLog("[HOME] 로봇이 연결되어 있지 않음");
            return;
        }

        // 이미 동작 중이면 무시
        if (isBusy)
        {
            WriteLog("[HOME] 이미 동작 중 - 무시");
            return;
        }

        // 동작 시작
        SetBusy(true);
        isMovingToHome = true;

        try
        {
            WriteLog($"[HOME] 홈 포지션으로 이동 시작: [{string.Join(", ", homePosition)}]");
            UpdateStatus("홈 포지션 이동 중...");

            // 도 단위를 라디안으로 변환
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            double[] anglesRad = new double[6];
            for (int i = 0; i < 6 && i < homePosition.Length; i++)
            {
                anglesRad[i] = homePosition[i] * Mathf.Deg2Rad;
            }

            string jointArray = string.Format(culture, "[{0:F6}, {1:F6}, {2:F6}, {3:F6}, {4:F6}, {5:F6}]",
                anglesRad[0], anglesRad[1], anglesRad[2], anglesRad[3], anglesRad[4], anglesRad[5]);

            string vStr = velocity.ToString("F2", culture);
            string aStr = acceleration.ToString("F2", culture);
            string tStr = homeMoveTime.ToString("F2", culture);

            // move_joint with t parameter (시간 기반 이동)
            string paramsJson = $"[{{\"pose\": {{\"kind\": 1, \"joint\": {{\"joint\": {jointArray}}}}}, \"param\": {{\"velocity\": {vStr}, \"acc\": {aStr}, \"t\": {tStr}}}}}]";

            WriteLog($"[HOME] move_joint 전송 (t={tStr}초): {paramsJson}");
            await SendJsonRpc("move_joint", paramsJson);

            // UI 슬라이더 업데이트 (이벤트 발생하지만 isMovingToHome이 true라 무시됨)
            for (int i = 0; i < 6 && i < sliders.Length && i < homePosition.Length; i++)
            {
                if (sliders[i] != null)
                {
                    sliders[i].value = homePosition[i];
                }
            }
            UpdateAllAngleTexts();

            // 로봇이 IDLE 상태가 될 때까지 대기
            await WaitUntilRobotIdle();

            // 그리퍼도 0%로 닫기
            WriteLog("[HOME] 그리퍼 닫기 (0%)");
            string gripperParams = "[{\"amplitude\": 0, \"force\": 50}]";
            await SendJsonRpc("set_claw", gripperParams);

            // 그리퍼 UI 업데이트
            if (gripperPositionSlider != null) gripperPositionSlider.value = 0;
            if (gripperForceSlider != null) gripperForceSlider.value = 50;
            UpdateGripperTexts();

            WriteLog("[HOME] 홈 포지션 이동 완료");
            UpdateStatus($"연결됨: {robotIP}:{robotPort}");
        }
        finally
        {
            // 홈 이동 완료 - 슬라이더 이벤트 다시 활성화
            isMovingToHome = false;
            SetBusy(false);
        }
    }

    #endregion

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
        // 티칭 중이면 취소
        if (isTeachingRunning)
        {
            StopTeaching();
        }
        WriteLog("=== Lebai Robot Controller 종료 ===");
    }

    #region 티칭 시스템

    /// <summary>
    /// 티칭 상태 텍스트 업데이트
    /// </summary>
    private void UpdateTeachingStatus(string message)
    {
        if (teachingStatusText != null)
        {
            teachingStatusText.text = message;
        }
        WriteLog($"[TEACHING] {message}");
    }

    /// <summary>
    /// JSON 파일 경로 가져오기 (빌드/에디터 모두 지원)
    /// </summary>
    private string GetTeachingFilePath()
    {
        string appPath = Application.dataPath;
        string baseDir = Application.isEditor
            ? Directory.GetParent(appPath).FullName  // 에디터: 프로젝트 루트
            : Directory.GetParent(appPath).FullName; // 빌드: exe 파일 위치

        return Path.Combine(baseDir, teachingFileName);
    }

    /// <summary>
    /// 티칭 UI 활성화/비활성화
    /// </summary>
    /// <param name="active">티칭 UI 활성화 여부</param>
    /// <param name="affectControlPanel">controlPanel 상태도 변경할지 여부 (Start에서는 false)</param>
    private void SetTeachingUIActive(bool active, bool affectControlPanel = true)
    {
        if (stopTeachingButton != null)
            stopTeachingButton.gameObject.SetActive(active);
        if (teachingStatusText != null)
            teachingStatusText.gameObject.SetActive(active);

        // 티칭 중 인디케이터 활성화/비활성화
        if (teachingActiveIndicator != null)
            teachingActiveIndicator.SetActive(active);

        // 조작 UI 비활성화/활성화 (티칭 중에는 조작 불가)
        if (affectControlPanel && controlPanel != null)
            controlPanel.SetActive(!active);
    }

    /// <summary>
    /// 티칭 시작 버튼 클릭
    /// </summary>
    public async void StartTeaching()
    {
        if (!isConnected)
        {
            UpdateTeachingStatus("로봇 연결 필요");
            return;
        }

        if (isTeachingRunning)
        {
            UpdateTeachingStatus("이미 실행 중");
            return;
        }

        // 티칭 UI 활성화
        SetTeachingUIActive(true);

        string filePath = GetTeachingFilePath();
        WriteLog($"[TEACHING] 파일 로드 시도: {filePath}");

        if (!File.Exists(filePath))
        {
            UpdateTeachingStatus($"파일 없음: {teachingFileName}");
            WriteLog($"[TEACHING] 파일을 찾을 수 없음: {filePath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            currentTeachingData = JsonUtility.FromJson<TeachingData>(json);

            if (currentTeachingData == null || currentTeachingData.steps == null || currentTeachingData.steps.Count == 0)
            {
                UpdateTeachingStatus("유효하지 않은 JSON");
                return;
            }

            WriteLog($"[TEACHING] 로드 완료: {currentTeachingData.name}, 스텝 수: {currentTeachingData.steps.Count}");

            // 티칭 실행 시작
            teachingCancellation = new CancellationTokenSource();
            isTeachingRunning = true;

            await ExecuteTeaching(teachingCancellation.Token);
        }
        catch (Exception e)
        {
            UpdateTeachingStatus($"JSON 파싱 오류");
            WriteLog($"[TEACHING] JSON 파싱 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 티칭 중지 버튼 클릭
    /// </summary>
    public void StopTeaching()
    {
        if (!isTeachingRunning) return;

        WriteLog("[TEACHING] 사용자가 티칭 중지 요청");
        teachingCancellation?.Cancel();
        isTeachingRunning = false;

        // 로봇 정지
        StopRobot();

        UpdateTeachingStatus("사용자가 중지함");

        // 티칭 UI 비활성화
        SetTeachingUIActive(false);
    }

    /// <summary>
    /// 티칭 실행 (비동기)
    /// </summary>
    private async Task ExecuteTeaching(CancellationToken cancellationToken)
    {
        teachingStartTime = Time.time;
        UpdateTeachingStatus($"시작: {currentTeachingData.name}");

        // 스텝을 시간순으로 정렬
        currentTeachingData.steps.Sort((a, b) => a.time.CompareTo(b.time));

        foreach (var step in currentTeachingData.steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                WriteLog("[TEACHING] 취소됨");
                break;
            }

            // 스텝 시작 시간까지 대기
            float elapsedTime = Time.time - teachingStartTime;
            float waitTime = step.time - elapsedTime;

            if (waitTime > 0)
            {
                UpdateTeachingStatus($"대기 중... ({waitTime:F1}초 후 스텝 {step.stepNumber})");
                await Task.Delay((int)(waitTime * 1000), cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) break;

            // 스텝 실행
            UpdateTeachingStatus($"스텝 {step.stepNumber}: {step.name}");
            await ExecuteTeachingStep(step);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            float totalTime = Time.time - teachingStartTime;
            UpdateTeachingStatus($"완료! (총 {totalTime:F1}초)");

            // 완료 후 잠시 대기 후 UI 비활성화
            await Task.Delay(2000);
            SetTeachingUIActive(false);
        }

        isTeachingRunning = false;
    }

    /// <summary>
    /// 개별 티칭 스텝 실행
    /// </summary>
    private async Task ExecuteTeachingStep(TeachingStep step)
    {
        WriteLog($"[TEACHING] 스텝 {step.stepNumber} 실행: {step.name}, 타입: {step.action.type}");

        switch (step.action.type.ToLower())
        {
            case "move_joint":
                await ExecuteMoveJoint(step);
                break;

            case "set_gripper":
                await ExecuteSetGripper(step);
                break;

            case "set_do":
                await ExecuteSetDO(step);
                break;

            case "wait":
                // duration 동안 대기
                await Task.Delay((int)(step.duration * 1000));
                break;

            default:
                WriteLog($"[TEACHING] 알 수 없는 액션 타입: {step.action.type}");
                break;
        }
    }

    /// <summary>
    /// move_joint 액션 실행 (t 파라미터로 정확한 시간 제어)
    /// </summary>
    private async Task ExecuteMoveJoint(TeachingStep step)
    {
        if (step.action.joints == null || step.action.joints.Length < 6)
        {
            WriteLog("[TEACHING] joints 배열이 올바르지 않음");
            return;
        }

        // 도 단위를 라디안으로 변환
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        double[] anglesRad = new double[6];
        for (int i = 0; i < 6; i++)
        {
            anglesRad[i] = step.action.joints[i] * Mathf.Deg2Rad;
        }

        string jointArray = string.Format(culture, "[{0:F6}, {1:F6}, {2:F6}, {3:F6}, {4:F6}, {5:F6}]",
            anglesRad[0], anglesRad[1], anglesRad[2], anglesRad[3], anglesRad[4], anglesRad[5]);

        // 속도/가속도 (오버라이드 또는 기본값 사용)
        float v = step.action.velocity >= 0 ? step.action.velocity : velocity;
        float a = step.action.acceleration >= 0 ? step.action.acceleration : acceleration;
        string vStr = v.ToString("F2", culture);
        string aStr = a.ToString("F2", culture);

        // t 파라미터: duration을 사용하여 정확한 시간 제어
        string tStr = step.duration.ToString("F2", culture);

        // move_joint with t parameter (시간 기반 이동)
        string paramsJson = $"[{{\"pose\": {{\"kind\": 1, \"joint\": {{\"joint\": {jointArray}}}}}, \"param\": {{\"velocity\": {vStr}, \"acc\": {aStr}, \"t\": {tStr}}}}}]";

        WriteLog($"[TEACHING] move_joint 전송 (t={tStr}초): {paramsJson}");
        await SendJsonRpc("move_joint", paramsJson);

        // 로봇이 실제로 이동을 완료할 때까지 대기 (duration 만큼)
        int waitMs = (int)(step.duration * 1000);
        WriteLog($"[TEACHING] 이동 완료 대기: {step.duration}초");
        await Task.Delay(waitMs);

        // UI 슬라이더 업데이트 (메인 스레드에서)
        for (int i = 0; i < 6 && i < sliders.Length; i++)
        {
            if (sliders[i] != null)
            {
                sliders[i].value = step.action.joints[i];
            }
        }
        UpdateAllAngleTexts();
    }

    /// <summary>
    /// set_gripper 액션 실행
    /// </summary>
    private async Task ExecuteSetGripper(TeachingStep step)
    {
        float position = step.action.gripperPosition >= 0 ? step.action.gripperPosition : 0f;
        float force = step.action.gripperForce >= 0 ? step.action.gripperForce : 50f;

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string amplitude = position.ToString("F1", culture);
        string forceStr = force.ToString("F1", culture);

        string paramsJson = $"[{{\"amplitude\": {amplitude}, \"force\": {forceStr}}}]";
        WriteLog($"[TEACHING] set_claw 전송: {paramsJson}");
        await SendJsonRpc("set_claw", paramsJson);

        // UI 업데이트
        if (gripperPositionSlider != null) gripperPositionSlider.value = position;
        if (gripperForceSlider != null) gripperForceSlider.value = force;
        UpdateGripperTexts();

        // 그리퍼 동작 대기 (duration만큼)
        if (step.duration > 0)
        {
            await Task.Delay((int)(step.duration * 1000));
        }
    }

    /// <summary>
    /// set_do 액션 실행 (디지털 출력 제어 - 버큠 그리퍼 등)
    /// </summary>
    private async Task ExecuteSetDO(TeachingStep step)
    {
        string device = step.action.device ?? "FLANGE";
        int pin = step.action.pin;
        int value = step.action.value;

        // device를 대문자로 변환
        device = device.ToUpper();

        string paramsJson = $"[{{\"device\": \"{device}\", \"pin\": {pin}, \"value\": {value}}}]";
        WriteLog($"[TEACHING] set_do 전송: {paramsJson}");
        await SendJsonRpc("set_do", paramsJson);

        // DO 동작 대기 (duration만큼)
        if (step.duration > 0)
        {
            await Task.Delay((int)(step.duration * 1000));
        }
    }

    #endregion
}
