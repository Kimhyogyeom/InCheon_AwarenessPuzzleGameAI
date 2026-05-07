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

    [Header("UI - 버큠 그리퍼")]
    [SerializeField] private Button btnVacuumOn;
    [SerializeField] private Button btnVacuumOff;
    [SerializeField] private TextMeshProUGUI vacuumStatusText;
    [SerializeField] private string vacuumDevice = "FLANGE";  // FLANGE, ROBOT, EXTRA
    [SerializeField] private int vacuumPin = 0;  // DO 핀 번호
    private bool isVacuumOn = false;

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
    [SerializeField] private Button teaching1Button;  // 티칭 1 버튼
    [SerializeField] private Button teaching1ReverseButton;  // 티칭 1 리버스 버튼
    [SerializeField] private Button teaching2Button;  // 티칭 2 버튼
    [SerializeField] private Button teaching2ReverseButton;  // 티칭 2 리버스 버튼
    [SerializeField] private Button teaching3Button;  // 티칭 3 버튼
    [SerializeField] private Button teaching3ReverseButton;  // 티칭 3 리버스 버튼
    [SerializeField] private Button teaching4Button;  // 티칭 4 버튼
    [SerializeField] private Button teaching4ReverseButton;  // 티칭 4 리버스 버튼
    [SerializeField] private Button teaching5Button;  // 티칭 5 버튼
    [SerializeField] private Button teaching5ReverseButton;  // 티칭 5 리버스 버튼
    [SerializeField] private Button teaching6Button;  // 티칭 6 버튼
    [SerializeField] private Button teaching6ReverseButton;  // 티칭 6 리버스 버튼
    [SerializeField] private Button teaching7Button;  // 티칭 7 버튼
    [SerializeField] private Button teaching7ReverseButton;  // 티칭 7 리버스 버튼
    [SerializeField] private Button teaching8Button;  // 티칭 8 버튼
    [SerializeField] private Button teaching8ReverseButton;  // 티칭 8 리버스 버튼
    [SerializeField] private Button teaching9Button;  // 티칭 9 버튼
    [SerializeField] private Button teaching9ReverseButton;  // 티칭 9 리버스 버튼
    [SerializeField] private Button teaching10Button;  // 티칭 10 버튼
    [SerializeField] private Button teaching10ReverseButton;  // 티칭 10 리버스 버튼
    [SerializeField] private Button teaching11Button;  // 티칭 11 버튼
    [SerializeField] private Button teaching11ReverseButton;  // 티칭 11 리버스 버튼
    [SerializeField] private Button teaching12Button;  // 티칭 12 버튼
    [SerializeField] private Button teaching12ReverseButton;  // 티칭 12 리버스 버튼
    [SerializeField] private Button teaching13Button;  // 티칭 13 버튼
    [SerializeField] private Button teaching13ReverseButton;  // 티칭 13 리버스 버튼
    [SerializeField] private Button teaching14Button;  // 티칭 14 버튼
    [SerializeField] private Button teaching14ReverseButton;  // 티칭 14 리버스 버튼
    [SerializeField] private Button teaching15Button;  // 티칭 15 버튼
    [SerializeField] private Button teaching15ReverseButton;  // 티칭 15 리버스 버튼
    [SerializeField] private Button teaching16Button;  // 티칭 16 버튼
    [SerializeField] private Button teaching16ReverseButton;  // 티칭 16 리버스 버튼
    [SerializeField] private Button allTeachingsButton;  // 티칭 1~16 전체 실행 버튼
    [SerializeField] private Button allReverseTeachingsButton;  // 리버스 1~16 전체 실행 버튼
    [SerializeField] private Button teachingLoopButton;  // 티칭↔리버스 반복 버튼
    [SerializeField] private TextMeshProUGUI teachingStatusText;  // 티칭 상태 표시
    [SerializeField] private string teachingFileName = "robot_teaching.json";  // JSON 파일 이름
    [SerializeField] private GameObject teachingActiveIndicator;  // 티칭 중 활성화되는 오브젝트

    [Header("홈 포지션 설정")]
    [SerializeField] private float[] homePosition = new float[] { 0f, -45f, 90f, -45f, 0f, 0f };  // 홈 포지션 (도 단위)
    [SerializeField] private float homeMoveTime = 5f;  // 홈 포지션 이동 시간 (초)
    [SerializeField] private bool autoHomeOnConnect = true;  // 연결 시 자동으로 홈 포지션 이동

    [Header("속도 설정")]
    [SerializeField, Range(1f, 20f)] private float gameSpeedMultiplier = 1f;  // 게임 모드 배속 (2 = 2배속, 10+ = 대기시간 거의 제거)
    [SerializeField] private bool slowMode = false;  // true: 모든 티칭 속도 1/3로 감속
    [SerializeField] private Button slowModeToggleButton;  // 슬로우 모드 토글 버튼
    [SerializeField] private TextMeshProUGUI slowModeButtonText;  // 버튼 텍스트 표시

    private const float JOINT_LIMIT_DEG = 200f;

    [SerializeField] private float velocity = 0.5f;      // 기본 속도 (rad/s) - 권장: 0.5~3.0
    [SerializeField] private float acceleration = 1.0f;  // 기본 가속도 (rad/s²) - 권장: 0.5~5.0

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

    // 시작 시 init 완료 여부 (StartupLoadingOverlay용)
    public bool IsInitComplete { get; private set; } = false;

    // 티칭 시스템 상태
    private bool isTeachingRunning = false;
    public bool IsTeachingRunning => isTeachingRunning; // 외부에서 확인용
    private CancellationTokenSource teachingCancellation;
    private TeachingData currentTeachingData;
    private float teachingStartTime;
    private bool isGameModeTeaching = false;  // 게임 모드에서 시작된 티칭인지 여부
    private int lastCompletedTeachingNumber = 0;  // 마지막으로 완료된 티칭 번호 (게임 모드용)
    private bool stopAfterCurrentTeaching = false;  // 현재 티칭 완료 후 중지 플래그
    private float allTeachingsStartTime = 0f;  // 전체 티칭 시작 시간

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

        // 버큠 그리퍼 버튼 연결
        if (btnVacuumOn != null)
        {
            btnVacuumOn.onClick.AddListener(VacuumOn);
            WriteLog("[INIT] btnVacuumOn 연결됨");
        }
        if (btnVacuumOff != null)
        {
            btnVacuumOff.onClick.AddListener(VacuumOff);
            WriteLog("[INIT] btnVacuumOff 연결됨");
        }
        UpdateVacuumStatus();

        UpdateStatus("연결 안됨");
        UpdateAllAngleTexts();
        UpdateGripperTexts();
        UpdateVelocityAccelerationTexts();
        UpdateTeachingStatus("대기 중");

        // 티칭 UI 초기 상태: 비활성화 (controlPanel 상태는 유지)
        SetTeachingUIActive(false, affectControlPanel: false);

        // 슬로우 모드 버튼 초기 텍스트
        UpdateSlowModeText();

        // 자동 연결 시도
        WriteLog("[INIT] 자동 연결 시도...");
        Connect();
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

        // 슬라이더 값 변경 시 OnSliderChanged가 호출되어 SendJointMove가 자동으로 실행됨
        // 버튼 클릭 시에는 스로틀링 없이 즉시 전송하도록 lastSliderSendTime 리셋
        lastSliderSendTime = 0f;
        sliders[jointIndex].value = newValue;
        // 주의: 여기서 SendJointMove()를 다시 호출하면 중복 전송됨
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

    [Header("자동 IP 스캔 설정")]
    [SerializeField] private bool autoScanIP = true;  // IP 자동 스캔 활성화
    [SerializeField] private string ipPrefix = "192.168.0.";  // IP 대역
    [SerializeField] private int ipRangeStart = 1;  // 스캔 시작 번호
    [SerializeField] private int ipRangeEnd = 20;   // 스캔 끝 번호

    public async void Connect()
    {
        // 이미 동작 중이면 무시
        if (isBusy)
        {
            WriteLog("[CONNECT] 이미 동작 중 - 무시");
            return;
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

        string ip = robotIP;

        // IP 입력이 있으면 그 IP 사용, 없으면 자동 스캔
        if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
        {
            ip = ipInputField.text;
        }
        else if (autoScanIP)
        {
            // 자동 IP 스캔
            UpdateStatus("로봇 검색 중...");
            string foundIP = await ScanForRobot(port);
            if (foundIP != null)
            {
                ip = foundIP;
                // 찾은 IP를 InputField에 표시
                if (ipInputField != null)
                {
                    ipInputField.text = ip;
                }
            }
            else
            {
                WriteLog("[CONNECT] 자동 스캔 실패 - 로봇을 찾을 수 없음");
                UpdateStatus("로봇을 찾을 수 없음");
                if (connectLoadingIndicator != null) connectLoadingIndicator.SetActive(false);
                return;
            }
        }

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
                robotIP = ip;  // 찾은 IP 저장
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

                // Init 완료 (StartupLoadingOverlay에 알림)
                IsInitComplete = true;
                WriteLog("[CONNECT] Init 완료 - 오버레이 해제 가능");
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
        // 게임 모드 중에는 개발자 패널 열기 차단
        if (isGameModeTeaching) return;

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

        // 티칭 실행 중이면 무시 (게임 중 홈 이동 방지)
        if (isTeachingRunning)
        {
            WriteLog("[HOME] 티칭 실행 중 - 홈 이동 스킵");
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
            WriteLog($"[HOME] 홈 포지션으로 이동 시작 (순차: J2→-90 > J3456 > J1 > J2→-45)");
            UpdateStatus("홈 포지션 이동 중...");

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            string vStr = velocity.ToString("F2", culture);
            string aStr = acceleration.ToString("F2", culture);

            // 현재 로봇 위치 동기화 (슬라이더가 0일 수 있으므로 반드시 갱신)
            await GetCurrentJointPositionsAsync();

            // 현재 각도 (슬라이더 값 사용)
            float[] currentAngles = new float[6];
            for (int i = 0; i < 6; i++)
            {
                currentAngles[i] = (sliders[i] != null) ? sliders[i].value : 0;
            }

            // STEP 1: J2축을 -90도로 리프트 (안전 이동 높이)
            WriteLog("[HOME] 1단계: J2축을 -90도로 리프트");
            currentAngles[1] = -90f;
            await MoveJointToAngles(currentAngles, vStr, aStr, "1.5");

            // STEP 2: J3,J4,J5,J6을 홈 포지션으로 이동 (J1 유지, J2=-90)
            WriteLog("[HOME] 2단계: J3,J4,J5,J6 홈 포지션으로 이동");
            currentAngles[2] = homePosition[2];  // 90
            currentAngles[3] = homePosition[3];  // -45
            currentAngles[4] = homePosition[4];  // 0
            currentAngles[5] = homePosition[5];  // 0
            await MoveJointToAngles(currentAngles, vStr, aStr, "2.0");

            // STEP 3: J1을 홈 포지션으로 이동 (J2=-90 유지)
            WriteLog("[HOME] 3단계: J1 홈 포지션으로 이동");
            currentAngles[0] = homePosition[0];  // 0
            await MoveJointToAngles(currentAngles, vStr, aStr, "1.5");

            // STEP 4: J2를 홈 포지션으로 하강 (-45도)
            WriteLog("[HOME] 4단계: J2 홈 포지션으로 하강");
            currentAngles[1] = homePosition[1];  // -45
            await MoveJointToAngles(currentAngles, vStr, aStr, "1.5");

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

    /// <summary>
    /// Helper 함수: 지정된 각도로 로봇 이동 (WaitUntilRobotIdle 포함)
    /// </summary>
    private async Task MoveJointToAngles(float[] angles, string vStr, string aStr, string tStr)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        double[] anglesRad = new double[6];
        for (int i = 0; i < 6; i++)
        {
            anglesRad[i] = angles[i] * Mathf.Deg2Rad;
        }

        string jointArray = string.Format(culture, "[{0:F6}, {1:F6}, {2:F6}, {3:F6}, {4:F6}, {5:F6}]",
            anglesRad[0], anglesRad[1], anglesRad[2], anglesRad[3], anglesRad[4], anglesRad[5]);

        string paramsJson = $"[{{\"pose\": {{\"kind\": 1, \"joint\": {{\"joint\": {jointArray}}}}}, \"param\": {{\"velocity\": {vStr}, \"acc\": {aStr}, \"t\": {tStr}}}}}]";

        await SendJsonRpc("move_joint", paramsJson);
        await WaitUntilRobotIdle();

        // UI 슬라이더 업데이트
        for (int i = 0; i < 6 && i < sliders.Length; i++)
        {
            if (sliders[i] != null)
            {
                sliders[i].value = angles[i];
            }
        }
        UpdateAllAngleTexts();
    }

    #endregion

    #region IP 자동 스캔

    /// <summary>
    /// IP 대역을 스캔해서 로봇을 찾음
    /// </summary>
    private async Task<string> ScanForRobot(int port)
    {
        WriteLog($"[SCAN] IP 스캔 시작: {ipPrefix}{ipRangeStart} ~ {ipPrefix}{ipRangeEnd}");

        // 빠른 스캔을 위한 짧은 타임아웃
        var scanClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(1) };

        // 병렬로 모든 IP 스캔
        var tasks = new List<Task<string>>();
        for (int i = ipRangeStart; i <= ipRangeEnd; i++)
        {
            string testIP = $"{ipPrefix}{i}";
            tasks.Add(TryConnectToIP(scanClient, testIP, port));
        }

        // 모든 스캔 완료 대기
        string[] results = await Task.WhenAll(tasks);

        // 성공한 IP 찾기
        foreach (string result in results)
        {
            if (result != null)
            {
                WriteLog($"[SCAN] 로봇 발견: {result}");
                scanClient.Dispose();
                return result;
            }
        }

        scanClient.Dispose();
        WriteLog("[SCAN] 로봇을 찾지 못함");
        return null;
    }

    /// <summary>
    /// 특정 IP에 로봇이 있는지 확인
    /// </summary>
    private async Task<string> TryConnectToIP(HttpClient client, string ip, int port)
    {
        try
        {
            string url = $"http://{ip}:{port}";
            string jsonBody = $"{{\"jsonrpc\":\"2.0\",\"method\":\"get_robot_state\",\"params\":[{{}}],\"id\":1}}";

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                if (responseBody.Contains("result"))
                {
                    WriteLog($"[SCAN] {ip} - 로봇 응답 확인");
                    return ip;
                }
            }
        }
        catch
        {
            // 연결 실패 - 무시
        }

        return null;
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
        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;

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

        // 조작 UI 비활성화/활성화 (티칭 중에는 조작 불가, 게임 모드에서는 항상 숨김)
        if (affectControlPanel && controlPanel != null)
        {
            if (isGameModeTeaching)
                controlPanel.SetActive(false);
            else
                controlPanel.SetActive(!active);
        }
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
    /// 티칭 1 버튼 클릭 (robot_teaching_1.json 실행)
    /// </summary>
    public void StartTeaching1()
    {
        StartTeachingByNumber(1);
    }

    /// <summary>
    /// 티칭 1 리버스 버튼 클릭 (robot_teaching_1_reverse.json 실행)
    /// 플레이스 위치에서 픽업하여 원래 위치로 되돌리기
    /// </summary>
    public void StartTeaching1Reverse()
    {
        StartTeachingByFileName("robot_teaching_1_reverse.json");
    }

    /// <summary>
    /// 티칭 2 버튼 클릭 (robot_teaching_2.json 실행)
    /// </summary>
    public void StartTeaching2()
    {
        StartTeachingByFileName("robot_teaching_2.json");
    }

    /// <summary>
    /// 티칭 2 리버스 버튼 클릭 (robot_teaching_2_reverse.json 실행)
    /// 플레이스 위치에서 픽업하여 원래 위치로 되돌리기
    /// </summary>
    public void StartTeaching2Reverse()
    {
        StartTeachingByFileName("robot_teaching_2_reverse.json");
    }

    /// <summary>
    /// 티칭 3 버튼 클릭 (robot_teaching_3.json 실행)
    /// </summary>
    public void StartTeaching3()
    {
        StartTeachingByFileName("robot_teaching_3.json");
    }

    /// <summary>
    /// 티칭 3 리버스 버튼 클릭 (robot_teaching_3_reverse.json 실행)
    /// 플레이스 위치에서 픽업하여 원래 위치로 되돌리기
    /// </summary>
    public void StartTeaching3Reverse()
    {
        StartTeachingByFileName("robot_teaching_3_reverse.json");
    }

    /// <summary>
    /// 티칭 4 버튼 클릭 (robot_teaching_4.json 실행)
    /// </summary>
    public void StartTeaching4()
    {
        StartTeachingByFileName("robot_teaching_4.json");
    }

    /// <summary>
    /// 티칭 4 리버스 버튼 클릭 (robot_teaching_4_reverse.json 실행)
    /// 플레이스 위치에서 픽업하여 원래 위치로 되돌리기
    /// </summary>
    public void StartTeaching4Reverse()
    {
        StartTeachingByFileName("robot_teaching_4_reverse.json");
    }

    /// <summary>
    /// 티칭 5 버튼 클릭 (robot_teaching_5.json 실행)
    /// </summary>
    public void StartTeaching5()
    {
        StartTeachingByFileName("robot_teaching_5.json");
    }

    /// <summary>
    /// 티칭 5 리버스 버튼 클릭 (robot_teaching_5_reverse.json 실행)
    /// 플레이스 위치에서 픽업하여 원래 위치로 되돌리기
    /// </summary>
    public void StartTeaching5Reverse()
    {
        StartTeachingByFileName("robot_teaching_5_reverse.json");
    }

    public void StartTeaching6()
    {
        StartTeachingByFileName("robot_teaching_6.json");
    }

    public void StartTeaching6Reverse()
    {
        StartTeachingByFileName("robot_teaching_6_reverse.json");
    }

    public void StartTeaching7()
    {
        StartTeachingByFileName("robot_teaching_7.json");
    }

    public void StartTeaching7Reverse()
    {
        StartTeachingByFileName("robot_teaching_7_reverse.json");
    }

    public void StartTeaching8()
    {
        StartTeachingByFileName("robot_teaching_8.json");
    }

    public void StartTeaching8Reverse()
    {
        StartTeachingByFileName("robot_teaching_8_reverse.json");
    }

    public void StartTeaching9()
    {
        StartTeachingByFileName("robot_teaching_9.json");
    }

    public void StartTeaching9Reverse()
    {
        StartTeachingByFileName("robot_teaching_9_reverse.json");
    }

    public void StartTeaching10()
    {
        StartTeachingByFileName("robot_teaching_10.json");
    }

    public void StartTeaching10Reverse()
    {
        StartTeachingByFileName("robot_teaching_10_reverse.json");
    }

    public void StartTeaching11()
    {
        StartTeachingByFileName("robot_teaching_11.json");
    }

    public void StartTeaching11Reverse()
    {
        StartTeachingByFileName("robot_teaching_11_reverse.json");
    }

    public void StartTeaching12()
    {
        StartTeachingByFileName("robot_teaching_12.json");
    }

    public void StartTeaching12Reverse()
    {
        StartTeachingByFileName("robot_teaching_12_reverse.json");
    }

    public void StartTeaching13()
    {
        StartTeachingByFileName("robot_teaching_13.json");
    }

    public void StartTeaching13Reverse()
    {
        StartTeachingByFileName("robot_teaching_13_reverse.json");
    }

    public void StartTeaching14()
    {
        StartTeachingByFileName("robot_teaching_14.json");
    }

    public void StartTeaching14Reverse()
    {
        StartTeachingByFileName("robot_teaching_14_reverse.json");
    }

    public void StartTeaching15()
    {
        StartTeachingByFileName("robot_teaching_15.json");
    }

    public void StartTeaching15Reverse()
    {
        StartTeachingByFileName("robot_teaching_15_reverse.json");
    }

    public void StartTeaching16()
    {
        StartTeachingByFileName("robot_teaching_16.json");
    }

    public void StartTeaching16Reverse()
    {
        StartTeachingByFileName("robot_teaching_16_reverse.json");
    }

    /// <summary>
    /// 티칭 1~16 전체 순차 실행
    /// </summary>
    public async void StartAllTeachings()
    {
        await RunTeachingsSequential(false);
    }

    /// <summary>
    /// 리버스 1~16 전체 순차 실행
    /// </summary>
    public async void StartAllReverseTeachings()
    {
        await RunTeachingsSequential(true);
    }

    /// <summary>
    /// 슬로우 모드 토글 (버튼 OnClick 연결용)
    /// </summary>
    public void ToggleSlowMode()
    {
        slowMode = !slowMode;
        UpdateSlowModeText();
        string state = slowMode ? "ON (1/3 속도)" : "OFF (최고 속도)";
        WriteLog($"[SLOW MODE] {state}");
        UpdateStatus($"슬로우 모드: {state}");
    }

    /// <summary>
    /// 슬로우 모드 버튼 텍스트 업데이트
    /// </summary>
    private void UpdateSlowModeText()
    {
        if (slowModeButtonText != null)
        {
            slowModeButtonText.text = slowMode ? "Speed Slow" : "Speed Normal";
        }
    }

    /// <summary>
    /// 티칭↔리버스 반복 실행 (T1→R1→T2→R2→...→T16→R16 → 다시 반복, 스톱까지)
    /// </summary>
    public async void StartTeachingLoop()
    {
        if (!isConnected)
        {
            UpdateTeachingStatus("로봇 연결 필요");
            return;
        }

        if (isTeachingRunning)
        {
            WriteLog("[TEACHING LOOP] 이미 티칭이 진행 중입니다.");
            return;
        }

        SetTeachingUIActive(true);
        teachingCancellation = new CancellationTokenSource();
        isTeachingRunning = true;
        allTeachingsStartTime = Time.time;
        int loopCount = 0;

        WriteLog("[TEACHING LOOP] 티칭↔리버스 반복 시작");

        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;

        try
        {
            while (!teachingCancellation.Token.IsCancellationRequested)
            {
                loopCount++;
                WriteLog($"[TEACHING LOOP] === 사이클 {loopCount} 시작 ===");

                for (int i = 1; i <= 16; i++)
                {
                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    // 티칭 i 실행
                    string teachFile = $"robot_teaching_{i}.json";
                    await ExecuteLoopFile(baseDir, teachFile, $"사이클{loopCount} 티칭{i}");

                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    // 다음 전 1초 대기
                    await Task.Delay(1000, teachingCancellation.Token);

                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    // 리버스 i 실행
                    string reverseFile = $"robot_teaching_{i}_reverse.json";
                    await ExecuteLoopFile(baseDir, reverseFile, $"사이클{loopCount} 리버스{i}");

                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    // 다음 전 1초 대기
                    if (i < 16)
                    {
                        await Task.Delay(1000, teachingCancellation.Token);
                    }
                }

                if (teachingCancellation.Token.IsCancellationRequested) break;

                // 사이클 간 2초 대기
                UpdateTeachingStatus($"사이클 {loopCount} 완료, 다음 사이클 준비...");
                await Task.Delay(2000, teachingCancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            WriteLog("[TEACHING LOOP] 취소됨");
        }

        float totalElapsed = Time.time - allTeachingsStartTime;
        UpdateTeachingStatus($"반복 종료 (사이클 {loopCount}, 총 {totalElapsed:F1}초)");
        WriteLog($"[TEACHING LOOP] 종료 (사이클 {loopCount}, 총 {totalElapsed:F1}초)");
        await Task.Delay(2000);

        SetTeachingUIActive(false);
        isTeachingRunning = false;
    }

    /// <summary>
    /// 반복 모드에서 단일 티칭 파일 실행 (내부)
    /// </summary>
    private async Task ExecuteLoopFile(string baseDir, string fileName, string label)
    {
        string filePath = Path.Combine(baseDir, fileName);

        if (!File.Exists(filePath))
        {
            WriteLog($"[TEACHING LOOP] 파일 없음: {fileName}, 건너뜀");
            return;
        }

        string json = File.ReadAllText(filePath);
        currentTeachingData = JsonUtility.FromJson<TeachingData>(json);

        if (currentTeachingData == null || currentTeachingData.steps == null || currentTeachingData.steps.Count == 0)
        {
            WriteLog($"[TEACHING LOOP] 유효하지 않은 JSON: {fileName}, 건너뜀");
            return;
        }

        WriteLog($"[TEACHING LOOP] {label} 시작: {currentTeachingData.name}");
        UpdateTeachingStatus($"{label}: {currentTeachingData.name}");

        teachingStartTime = Time.time;
        currentTeachingData.steps.Sort((a, b) => a.time.CompareTo(b.time));

        foreach (var step in currentTeachingData.steps)
        {
            if (teachingCancellation.Token.IsCancellationRequested) break;

            float elapsedTime = Time.time - teachingStartTime;
            float waitTime = step.time - elapsedTime;

            if (waitTime > 0)
            {
                UpdateTeachingStatus($"{label} - 스텝 {step.stepNumber} 대기");
                await Task.Delay((int)(waitTime * 1000), teachingCancellation.Token);
            }

            if (teachingCancellation.Token.IsCancellationRequested) break;

            UpdateTeachingStatus($"{label} - 스텝 {step.stepNumber}: {step.name}");
            await ExecuteTeachingStep(step);
        }

        WriteLog($"[TEACHING LOOP] {label} 완료");
    }

    /// <summary>
    /// 티칭 파일들을 순차적으로 실행 (1~16)
    /// </summary>
    private async Task RunTeachingsSequential(bool reverse)
    {
        if (!isConnected)
        {
            UpdateTeachingStatus("로봇 연결 필요");
            return;
        }

        if (isTeachingRunning)
        {
            WriteLog("[TEACHING] 이미 티칭이 진행 중입니다.");
            return;
        }

        SetTeachingUIActive(true);
        teachingCancellation = new CancellationTokenSource();
        isTeachingRunning = true;
        allTeachingsStartTime = Time.time;

        string label = reverse ? "리버스" : "티칭";
        WriteLog($"[TEACHING] 전체 {label} 1~16 순차 실행 시작");

        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;

        for (int i = 1; i <= 16; i++)
        {
            if (teachingCancellation.Token.IsCancellationRequested) break;

            string fileName = reverse
                ? $"robot_teaching_{i}_reverse.json"
                : $"robot_teaching_{i}.json";
            string filePath = Path.Combine(baseDir, fileName);

            UpdateTeachingStatus($"{label} {i}/16 로드 중...");

            if (!File.Exists(filePath))
            {
                WriteLog($"[TEACHING] 파일을 찾을 수 없습니다: {fileName}, 건너뜀");
                continue;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                currentTeachingData = JsonUtility.FromJson<TeachingData>(json);

                if (currentTeachingData == null || currentTeachingData.steps == null || currentTeachingData.steps.Count == 0)
                {
                    WriteLog($"[TEACHING] 유효하지 않은 JSON: {fileName}, 건너뜀");
                    continue;
                }

                WriteLog($"[TEACHING] [{i}/16] 로드 완료: {currentTeachingData.name}");
                UpdateTeachingStatus($"{label} {i}/16: {currentTeachingData.name}");

                // 개별 티칭 실행 (UI 완료 대기 없이)
                teachingStartTime = Time.time;
                currentTeachingData.steps.Sort((a, b) => a.time.CompareTo(b.time));

                foreach (var step in currentTeachingData.steps)
                {
                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    float elapsedTime = Time.time - teachingStartTime;
                    float waitTime = step.time - elapsedTime;

                    if (waitTime > 0)
                    {
                        UpdateTeachingStatus($"{label} {i}/16 - 스텝 {step.stepNumber} 대기 ({waitTime:F1}초)");
                        await Task.Delay((int)(waitTime * 1000), teachingCancellation.Token);
                    }

                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    UpdateTeachingStatus($"{label} {i}/16 - 스텝 {step.stepNumber}: {step.name}");
                    await ExecuteTeachingStep(step);
                }

                if (teachingCancellation.Token.IsCancellationRequested) break;

                WriteLog($"[TEACHING] [{i}/16] 완료: {currentTeachingData.name}");

                // 다음 티칭 전 1초 대기
                if (i < 16)
                {
                    UpdateTeachingStatus($"{label} {i}/16 완료, 다음 준비 중...");
                    await Task.Delay(1000, teachingCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                WriteLog($"[TEACHING] 전체 {label} 취소됨 ({i}/16에서)");
                break;
            }
            catch (Exception e)
            {
                WriteLog($"[TEACHING] {fileName} 오류: {e.Message}, 건너뜀");
                continue;
            }
        }

        float totalElapsed = Time.time - allTeachingsStartTime;

        if (!teachingCancellation.Token.IsCancellationRequested)
        {
            UpdateTeachingStatus($"전체 {label} 1~16 완료! (총 {totalElapsed:F1}초)");
            WriteLog($"[TEACHING] 전체 {label} 1~16 순차 실행 완료 (총 {totalElapsed:F1}초)");
            await Task.Delay(2000);
        }
        else
        {
            WriteLog($"[TEACHING] 전체 {label} 취소됨 (경과 {totalElapsed:F1}초)");
        }

        SetTeachingUIActive(false);
        isTeachingRunning = false;
    }

    /// <summary>
    /// 지정된 파일명으로 티칭 실행
    /// </summary>
    public async void StartTeachingByFileName(string fileName)
    {
        if (!isConnected)
        {
            UpdateTeachingStatus("로봇 연결 필요");
            return;
        }

        if (isTeachingRunning)
        {
            WriteLog("[TEACHING] 이미 티칭이 진행 중입니다.");
            return;
        }

        // 티칭 UI 활성화
        SetTeachingUIActive(true);

        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;
        string filePath = Path.Combine(baseDir, fileName);

        WriteLog($"[TEACHING] 파일 로드 시도: {filePath}");

        if (!File.Exists(filePath))
        {
            WriteLog($"[TEACHING] 파일을 찾을 수 없습니다: {fileName}");
            UpdateTeachingStatus($"파일 없음: {fileName}");
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
    /// 지정된 번호의 티칭 파일 실행
    /// </summary>
    public async void StartTeachingByNumber(int teachingNumber)
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

        string fileName = $"robot_teaching_{teachingNumber}.json";
        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;
        string filePath = Path.Combine(baseDir, fileName);

        WriteLog($"[TEACHING] 티칭 {teachingNumber} 파일 로드 시도: {filePath}");

        if (!File.Exists(filePath))
        {
            UpdateTeachingStatus($"파일 없음: {fileName}");
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

        // 로봇 정지
        StopRobot();

        UpdateTeachingStatus("사용자가 중지함");

        // 티칭 UI 비활성화 (게임 모드에서는 controlPanel에 영향 주지 않음)
        SetTeachingUIActive(false, affectControlPanel: !isGameModeTeaching);

        isTeachingRunning = false;
        isGameModeTeaching = false;  // 플래그 리셋
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
    private async Task ExecuteTeachingStep(TeachingStep step, float speedMultiplier = 1f)
    {
        WriteLog($"[TEACHING] 스텝 {step.stepNumber} 실행: {step.name}, 타입: {step.action.type}");

        switch (step.action.type.ToLower())
        {
            case "move_joint":
                await ExecuteMoveJoint(step, speedMultiplier);
                break;

            case "set_gripper":
                await ExecuteSetGripper(step);
                break;

            case "set_do":
                await ExecuteSetDO(step);
                break;

            case "wait":
                // duration 동안 대기 (배속 적용)
                float waitDuration = step.duration / Mathf.Max(speedMultiplier, 1f);
                await Task.Delay((int)(waitDuration * 1000));
                break;

            default:
                WriteLog($"[TEACHING] 알 수 없는 액션 타입: {step.action.type}");
                break;
        }
    }

    /// <summary>
    /// move_joint 액션 실행 (t 파라미터로 정확한 시간 제어)
    /// </summary>
    private async Task ExecuteMoveJoint(TeachingStep step, float speedMultiplier = 1f)
    {
        if (step.action.joints == null || step.action.joints.Length < 6)
        {
            WriteLog("[TEACHING] joints 배열이 올바르지 않음");
            return;
        }

        // 실제 사용할 joints 배열
        float[] jointsToUse = new float[6];
        for (int i = 0; i < 6; i++)
        {
            jointsToUse[i] = step.action.joints[i];
        }

        // "J2 리프트" 스텝: J2만 변경하고 나머지는 현재 위치 유지 (연속 티칭 시 0으로 가는 문제 해결)
        if (step.name != null && step.name.Contains("J2 리프트"))
        {
            // 현재 슬라이더 값으로 J1, J3, J4, J5, J6 유지
            for (int i = 0; i < 6; i++)
            {
                if (i != 1 && sliders[i] != null)  // J2(인덱스 1)는 JSON 값 사용
                {
                    jointsToUse[i] = sliders[i].value;
                }
            }
            WriteLog($"[TEACHING] J2 리프트: 현재 위치 유지, J2만 {step.action.joints[1]}도로 이동");
        }

        // 도 단위를 라디안으로 변환
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        double[] anglesRad = new double[6];
        for (int i = 0; i < 6; i++)
        {
            anglesRad[i] = jointsToUse[i] * Mathf.Deg2Rad;
        }

        string jointArray = string.Format(culture, "[{0:F6}, {1:F6}, {2:F6}, {3:F6}, {4:F6}, {5:F6}]",
            anglesRad[0], anglesRad[1], anglesRad[2], anglesRad[3], anglesRad[4], anglesRad[5]);

        // 속도/가속도 (오버라이드 또는 기본값 사용)
        float v = step.action.velocity >= 0 ? step.action.velocity : velocity;
        float a = step.action.acceleration >= 0 ? step.action.acceleration : acceleration;

        // 슬로우 모드: 속도/가속도 1/3
        if (slowMode)
        {
            v /= 3f;
            a /= 3f;
        }

        string vStr = v.ToString("F2", culture);
        string aStr = a.ToString("F2", culture);

        // t 파라미터: duration을 사용하여 정확한 시간 제어 (슬로우 모드: 3배, 배속 적용)
        float t = slowMode ? step.duration * 3f : step.duration / Mathf.Max(speedMultiplier, 1f);
        string tStr = t.ToString("F2", culture);

        // move_joint with t parameter (시간 기반 이동)
        string paramsJson = $"[{{\"pose\": {{\"kind\": 1, \"joint\": {{\"joint\": {jointArray}}}}}, \"param\": {{\"velocity\": {vStr}, \"acc\": {aStr}, \"t\": {tStr}}}}}]";

        WriteLog($"[TEACHING] move_joint 전송 (t={tStr}초): {paramsJson}");
        await SendJsonRpc("move_joint", paramsJson);

        // 로봇이 실제로 이동을 완료할 때까지 대기 (IDLE 상태까지)
        WriteLog($"[TEACHING] 로봇 IDLE 상태 대기 중...");
        await WaitUntilRobotIdle();

        // UI 슬라이더 업데이트 (실제 이동한 값으로)
        for (int i = 0; i < 6 && i < sliders.Length; i++)
        {
            if (sliders[i] != null)
            {
                sliders[i].value = jointsToUse[i];
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
        int pin = step.action.pin;
        int value = step.action.value;

        // 밸브(pin 0) 신호 반전 - 모터 교체로 배선이 바뀌어 ON/Blow가 뒤집힘
        if (pin == 0)
            value = (value == 0) ? 1 : 0;

        string paramsJson = $"[{{\"pin\": {pin}, \"value\": {value}}}]";

        // 밸브(pin 0) 제어 시 펌프(pin 1)도 같이 토글
        // 흡착(value=1)이면 펌프 먼저 ON 후 밸브 ON, 블로우/OFF(value=0)면 밸브 먼저 OFF 후 펌프 OFF
        if (pin == 0)
        {
            string pumpParams = $"[{{\"pin\": 1, \"value\": {value}}}]";
            if (value == 1)
            {
                WriteLog($"[TEACHING] set_do 흡착: 펌프 ON → 밸브 ON (value={value})");
                await SendJsonRpc("set_do", pumpParams);
                await SendJsonRpc("set_do", paramsJson);
            }
            else
            {
                WriteLog($"[TEACHING] set_do 블로우/OFF: 밸브 OFF → 펌프 OFF (value={value})");
                await SendJsonRpc("set_do", paramsJson);
                await SendJsonRpc("set_do", pumpParams);
            }
        }
        else
        {
            WriteLog($"[TEACHING] set_do 전송 (pin={pin}, value={value}): {paramsJson}");
            await SendJsonRpc("set_do", paramsJson);
        }

        // 밸브(pin 0) 동작 시 최소 시간 보장 - 흡착 1초, 블로우 0.2초
        float duration = step.duration;
        if (pin == 0)
        {
            float minDuration = (value == 1) ? 1.0f : 0.2f;  // 반전 후: 1=흡착(ON), 0=블로우
            if (duration < minDuration)
                duration = minDuration;
        }

        if (duration > 0)
        {
            await Task.Delay((int)(duration * 1000));
        }
    }

    #endregion

    #region 버큠 그리퍼 제어

    /// <summary>
    /// 버큠 ON (흡착)
    /// 문서: set_do(1, 1) 진공펌프 ON, set_do(0, 0) 밸브 OFF
    /// </summary>
    public async void VacuumOn()
    {
        if (!isConnected)
        {
            WriteLog("[VACUUM] 로봇 연결 안됨");
            return;
        }

        WriteLog("[VACUUM] ON 요청 - 진공펌프 ON, 밸브 ON");

        // DO_1 = 1 (진공 펌프 ON)
        string paramsJson1 = $"[{{\"pin\": 1, \"value\": 1}}]";
        string response1 = await SendJsonRpc("set_do", paramsJson1);

        // DO_0 = 1 (밸브 ON - 모터 교체로 인해 반전)
        string paramsJson0 = $"[{{\"pin\": 0, \"value\": 1}}]";
        string response0 = await SendJsonRpc("set_do", paramsJson0);

        if (response1 != null && response1.Contains("result") &&
            response0 != null && response0.Contains("result"))
        {
            isVacuumOn = true;
            WriteLog("[VACUUM] ON 성공 (DO_1=1, DO_0=1)");
        }
        else
        {
            WriteLog($"[VACUUM] ON 실패 - DO_1: {response1}, DO_0: {response0}");
        }

        UpdateVacuumStatus();
    }

    /// <summary>
    /// 버큠 OFF (중료)
    /// 문서: set_do(1, 0) 진공펌프 OFF, set_do(0, 0) 밸브 OFF
    /// </summary>
    public async void VacuumOff()
    {
        if (!isConnected)
        {
            WriteLog("[VACUUM] 로봇 연결 안됨");
            return;
        }

        WriteLog("[VACUUM] OFF 요청 - 진공펌프 OFF, 밸브 OFF");

        // DO_1 = 0 (진공 펌프 OFF)
        string paramsJson1 = $"[{{\"pin\": 1, \"value\": 0}}]";
        string response1 = await SendJsonRpc("set_do", paramsJson1);

        // DO_0 = 0 (밸브 OFF)
        string paramsJson0 = $"[{{\"pin\": 0, \"value\": 0}}]";
        string response0 = await SendJsonRpc("set_do", paramsJson0);

        if (response1 != null && response1.Contains("result") &&
            response0 != null && response0.Contains("result"))
        {
            isVacuumOn = false;
            WriteLog("[VACUUM] OFF 성공 (DO_1=0, DO_0=0)");
        }
        else
        {
            WriteLog($"[VACUUM] OFF 실패 - DO_1: {response1}, DO_0: {response0}");
        }

        UpdateVacuumStatus();
    }

    /// <summary>
    /// 버큠 배출 (놓을 때 사용)
    /// 문서: set_do(1, 1) 진공펌프 ON, set_do(0, 1) 밸브 ON
    /// </summary>
    public async void VacuumBlow()
    {
        if (!isConnected)
        {
            WriteLog("[VACUUM] 로봇 연결 안됨");
            return;
        }

        WriteLog("[VACUUM] 배출 요청 - 진공펌프 ON, 밸브 OFF");

        // DO_1 = 1 (진공 펌프 ON)
        string paramsJson1 = $"[{{\"pin\": 1, \"value\": 1}}]";
        string response1 = await SendJsonRpc("set_do", paramsJson1);

        // DO_0 = 0 (밸브 OFF - 모터 교체로 인해 반전)
        string paramsJson0 = $"[{{\"pin\": 0, \"value\": 0}}]";
        string response0 = await SendJsonRpc("set_do", paramsJson0);

        if (response1 != null && response1.Contains("result") &&
            response0 != null && response0.Contains("result"))
        {
            WriteLog("[VACUUM] 배출 성공 (DO_1=1, DO_0=0)");
        }
        else
        {
            WriteLog($"[VACUUM] 배출 실패 - DO_1: {response1}, DO_0: {response0}");
        }
    }

    /// <summary>
    /// 버큠 상태 텍스트 업데이트
    /// </summary>
    private void UpdateVacuumStatus()
    {
        if (vacuumStatusText != null)
        {
            vacuumStatusText.text = isVacuumOn ? "ON" : "OFF";
            vacuumStatusText.color = isVacuumOn ? Color.green : Color.gray;
        }
    }

    #endregion

    #region 정리 티칭 (버튼 연결용)

    // 정리 티칭 완료 콜백
    private System.Action _onCleanupComplete;

    /// <summary>
    /// 정리 티칭 시작 - 버튼의 OnClick에 연결
    /// robot_cleanup.json 파일을 실행함
    /// </summary>
    public void StartCleanupTeaching()
    {
        StartCleanupTeachingWithCallback(null);
    }

    /// <summary>
    /// 정리 티칭 시작 (콜백 포함) - PuzzleManager에서 호출
    /// </summary>
    public void StartCleanupTeachingWithCallback(System.Action onComplete)
    {
        _onCleanupComplete = onComplete;

        if (!isConnected)
        {
            WriteLog("[CLEANUP] 로봇 연결 안됨 - 정리 티칭 스킵");
            UpdateStatus("로봇 연결 필요");
            _onCleanupComplete?.Invoke();
            return;
        }

        if (isTeachingRunning)
        {
            WriteLog("[CLEANUP] 이미 티칭 실행 중");
            return;
        }

        if (isBusy)
        {
            WriteLog("[CLEANUP] 로봇 동작 중 - 정리 티칭 스킵");
            return;
        }

        WriteLog("[CLEANUP] 정리 티칭 시작 요청");
        _ = ExecuteCleanupTeaching();
    }

    /// <summary>
    /// 정리 티칭 실행 (비동기) - UI 없이 실행
    /// </summary>
    private async Task ExecuteCleanupTeaching()
    {
        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;

        string filePath = Path.Combine(baseDir, "robot_cleanup.json");
        WriteLog($"[CLEANUP] 파일 로드 시도: {filePath}");

        if (!File.Exists(filePath))
        {
            WriteLog($"[CLEANUP] 파일을 찾을 수 없음: {filePath}");
            UpdateStatus("robot_cleanup.json 없음");
            _onCleanupComplete?.Invoke();
            _onCleanupComplete = null;
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            TeachingData cleanupData = JsonUtility.FromJson<TeachingData>(json);

            if (cleanupData == null || cleanupData.steps == null || cleanupData.steps.Count == 0)
            {
                WriteLog("[CLEANUP] 유효하지 않은 JSON");
                _onCleanupComplete?.Invoke();
                _onCleanupComplete = null;
                return;
            }

            WriteLog($"[CLEANUP] 로드 완료: {cleanupData.name}, 스텝 수: {cleanupData.steps.Count}");

            // 게임 모드 플래그 설정 (UI 영향 방지)
            isGameModeTeaching = true;

            // 티칭 실행 (UI 없이)
            currentTeachingData = cleanupData;
            teachingCancellation = new CancellationTokenSource();
            isTeachingRunning = true;

            await ExecuteTeachingWithoutUI(teachingCancellation.Token);

            WriteLog("[CLEANUP] 정리 티칭 완료");
        }
        catch (Exception e)
        {
            WriteLog($"[CLEANUP] 오류: {e.Message}");
            isGameModeTeaching = false;
        }

        // 완료 콜백 호출
        _onCleanupComplete?.Invoke();
        _onCleanupComplete = null;
    }

    #endregion

    #region 외부 연동 (PuzzleManager용)

    /// <summary>
    /// 티칭 1~16 전체 소요 시간 계산 (각 파일 duration 합 + 사이 대기 1초)
    /// </summary>
    public float GetTeachingDuration()
    {
        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;

        float totalDuration = 0f;
        int loadedCount = 0;

        for (int i = 1; i <= 16; i++)
        {
            string filePath = Path.Combine(baseDir, $"robot_teaching_{i}_reverse.json");

            if (!File.Exists(filePath)) continue;

            try
            {
                string json = File.ReadAllText(filePath);
                TeachingData data = JsonUtility.FromJson<TeachingData>(json);

                if (data != null && data.steps != null && data.steps.Count > 0)
                {
                    float fileEndTime = 0f;
                    foreach (var step in data.steps)
                    {
                        float stepEndTime = step.time + step.duration;
                        if (stepEndTime > fileEndTime)
                        {
                            fileEndTime = stepEndTime;
                        }
                    }

                    totalDuration += fileEndTime;
                    loadedCount++;
                }
            }
            catch (Exception e)
            {
                WriteLog($"[TEACHING] GetTeachingDuration: robot_teaching_{i}_reverse.json 오류: {e.Message}");
            }
        }

        // 티칭 사이 대기 1초 (마지막 제외)
        if (loadedCount > 1)
        {
            totalDuration += (loadedCount - 1) * 1.0f;
        }

        WriteLog($"[TEACHING] GetTeachingDuration: 리버스 1~16 총 {totalDuration:F1}초 ({loadedCount}개 로드)");
        return totalDuration > 0 ? totalDuration : -1f;
    }

    /// <summary>
    /// 로봇 연결 여부 확인
    /// </summary>
    public bool IsRobotConnected()
    {
        return isConnected;
    }

    /// <summary>
    /// 외부에서 티칭 시작 (게임 시작 시 호출)
    /// </summary>
    /// <param name="onComplete">티칭 완료 시 호출되는 콜백</param>
    public void StartTeachingExternal(System.Action onComplete = null)
    {
        _onTeachingComplete = onComplete;

        if (!isConnected)
        {
            WriteLog("[TEACHING] 외부 호출: 로봇 연결 안됨 - 스킵");
            _onTeachingComplete?.Invoke();
            return;
        }

        if (isTeachingRunning)
        {
            WriteLog("[TEACHING] 외부 호출: 이미 실행 중");
            return;
        }

        WriteLog("[TEACHING] 외부 호출: 게임 시작으로 티칭 시작");
        StartTeachingWithoutUI();
    }

    // 티칭 완료 콜백 (게임용)
    private System.Action _onTeachingComplete;

    /// <summary>
    /// UI 팝업 없이 티칭 시작 (게임 모드용)
    /// </summary>
    private async void StartTeachingWithoutUI()
    {
        if (!isConnected)
        {
            WriteLog("[TEACHING] StartTeachingWithoutUI: 로봇 연결 안됨");
            return;
        }

        if (isTeachingRunning)
        {
            WriteLog("[TEACHING] StartTeachingWithoutUI: 이미 실행 중");
            return;
        }

        // 게임 모드 플래그 설정 (UI 영향 방지)
        isGameModeTeaching = true;

        string filePath = GetTeachingFilePath();
        WriteLog($"[TEACHING] 파일 로드 시도 (UI 없음): {filePath}");

        if (!System.IO.File.Exists(filePath))
        {
            WriteLog($"[TEACHING] 파일을 찾을 수 없음: {filePath}");
            return;
        }

        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            currentTeachingData = JsonUtility.FromJson<TeachingData>(json);

            if (currentTeachingData == null || currentTeachingData.steps == null || currentTeachingData.steps.Count == 0)
            {
                WriteLog("[TEACHING] 유효하지 않은 JSON");
                return;
            }

            WriteLog($"[TEACHING] 로드 완료 (UI 없음): {currentTeachingData.name}, 스텝 수: {currentTeachingData.steps.Count}");

            // 티칭 실행 시작 (UI는 활성화하지 않음!)
            teachingCancellation = new CancellationTokenSource();
            isTeachingRunning = true;

            await ExecuteTeachingWithoutUI(teachingCancellation.Token);
        }
        catch (System.Exception e)
        {
            WriteLog($"[TEACHING] JSON 파싱 오류: {e.Message}");
        }
    }

    /// <summary>
    /// UI 없이 티칭 실행 (게임 모드용)
    /// </summary>
    private async Task ExecuteTeachingWithoutUI(CancellationToken cancellationToken)
    {
        teachingStartTime = Time.time;
        WriteLog($"[TEACHING] 시작 (UI 없음): {currentTeachingData.name}");

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
                await Task.Delay((int)(waitTime * 1000), cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) break;

            // 스텝 실행
            WriteLog($"[TEACHING] 스텝 {step.stepNumber}: {step.name}");
            await ExecuteTeachingStep(step);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            float totalTime = Time.time - teachingStartTime;
            WriteLog($"[TEACHING] 완료! (총 {totalTime:F1}초)");

            // 티칭 완료 콜백 호출 (로봇 승리 처리용)
            _onTeachingComplete?.Invoke();
            _onTeachingComplete = null;
        }

        isTeachingRunning = false;
        isGameModeTeaching = false;  // 플래그 리셋
    }

    /// <summary>
    /// 외부에서 티칭 중지 (게임 종료 시 호출)
    /// </summary>
    public void StopTeachingExternal()
    {
        if (isTeachingRunning)
        {
            WriteLog("[TEACHING] 외부 호출: 게임 종료로 티칭 중지");
            StopTeaching();
        }
    }

    /// <summary>
    /// 게임 모드: 티칭 1~16 순차 실행 (UI 없이)
    /// </summary>
    public void StartGameTeachings(System.Action onComplete = null)
    {
        // 게임 모드 진입 시 개발자 전용 컨트롤 패널 강제 숨김
        if (controlPanel != null)
            controlPanel.SetActive(false);

        _onTeachingComplete = onComplete;
        lastCompletedTeachingNumber = 0;
        stopAfterCurrentTeaching = false;

        if (!isConnected)
        {
            WriteLog("[GAME TEACHING] 로봇 연결 안됨 - 스킵 (콜백 호출 안 함)");
            // 안전장치: 로봇 연결 안됨 - 콜백 호출하지 않음 (게임이 즉시 종료되는 것 방지)
            _onTeachingComplete = null;
            return;
        }

        if (isTeachingRunning)
        {
            WriteLog("[GAME TEACHING] 이미 실행 중");
            return;
        }

        WriteLog("[GAME TEACHING] 게임 모드 티칭 1~16 순차 실행 시작");
        _ = RunGameTeachingsSequential();
    }

    /// <summary>
    /// 게임 모드: 현재 진행 중인 티칭 완료 후 중지 (즉시 취소하지 않음)
    /// </summary>
    public void StopGameTeachingsGraceful()
    {
        if (!isTeachingRunning)
        {
            WriteLog("[GAME TEACHING] 실행 중이 아님");
            return;
        }

        WriteLog($"[GAME TEACHING] 현재 티칭 완료 후 중지 요청 (마지막 완료: {lastCompletedTeachingNumber}번)");
        stopAfterCurrentTeaching = true;
    }

    /// <summary>
    /// 게임 모드: 완료된 티칭만 리버스 정리 (1~N번)
    /// </summary>
    public void StartGameCleanup(System.Action onComplete, System.Action<int, int> onProgress = null)
    {
        _ = StartGameCleanupAsync(onComplete, onProgress);
    }

    private async Task StartGameCleanupAsync(System.Action onComplete, System.Action<int, int> onProgress = null)
    {
        // 현재 티칭이 끝날 때까지 대기 (graceful stop 후 호출되므로)
        if (isTeachingRunning)
        {
            WriteLog("[GAME CLEANUP] 현재 티칭 완료 대기 중...");
            while (isTeachingRunning)
            {
                await Task.Delay(200);
            }
            WriteLog("[GAME CLEANUP] 티칭 완료 확인");
        }

        int cleanupCount = lastCompletedTeachingNumber;

        if (cleanupCount <= 0)
        {
            WriteLog("[GAME CLEANUP] 정리할 티칭 없음 (완료된 티칭 0개)");
            onComplete?.Invoke();
            return;
        }

        if (!isConnected)
        {
            WriteLog("[GAME CLEANUP] 로봇 연결 안됨 - 정리 스킵");
            onComplete?.Invoke();
            return;
        }

        WriteLog($"[GAME CLEANUP] 리버스 1~{cleanupCount} 정리 시작");
        _ = RunGameCleanupSequential(cleanupCount, onComplete, onProgress);
    }

    /// <summary>
    /// 마지막으로 완료된 티칭 번호 반환
    /// </summary>
    public int GetLastCompletedTeachingNumber()
    {
        return lastCompletedTeachingNumber;
    }

    /// <summary>
    /// 게임 모드 티칭 순차 실행 (내부)
    /// </summary>
    private async Task RunGameTeachingsSequential()
    {
        isGameModeTeaching = true;
        teachingCancellation = new CancellationTokenSource();
        isTeachingRunning = true;
        allTeachingsStartTime = Time.time;

        // 게임 시작 시 펌프/밸브 모두 OFF로 초기화 (흡착 시점에만 같이 켜짐)
        WriteLog("[GAME TEACHING] 펌프/밸브 OFF 초기화 (게임 시작)");
        await SendJsonRpc("set_do", "[{\"pin\": 0, \"value\": 0}]");  // 밸브 OFF (반전 후 블로우 방향)
        await SendJsonRpc("set_do", "[{\"pin\": 1, \"value\": 0}]");  // 펌프 OFF

        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;

        for (int i = 1; i <= 16; i++)
        {
            // 이전 티칭 완료 후 중지 플래그 체크
            if (stopAfterCurrentTeaching)
            {
                WriteLog($"[GAME TEACHING] 중지 플래그 감지 - {i}번 시작하지 않음 (완료: {lastCompletedTeachingNumber}번까지)");
                break;
            }

            if (teachingCancellation.Token.IsCancellationRequested) break;

            string fileName = $"robot_teaching_{i}_reverse.json";
            string filePath = Path.Combine(baseDir, fileName);

            if (!File.Exists(filePath))
            {
                WriteLog($"[GAME TEACHING] 파일 없음: {fileName}, 건너뜀");
                continue;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                currentTeachingData = JsonUtility.FromJson<TeachingData>(json);

                if (currentTeachingData == null || currentTeachingData.steps == null || currentTeachingData.steps.Count == 0)
                {
                    WriteLog($"[GAME TEACHING] 유효하지 않은 JSON: {fileName}, 건너뜀");
                    continue;
                }

                WriteLog($"[GAME TEACHING] 리버스 [{i}/16] 시작: {currentTeachingData.name}");

                // 개별 티칭 실행
                teachingStartTime = Time.time;
                currentTeachingData.steps.Sort((a, b) => a.time.CompareTo(b.time));

                float speedMul = Mathf.Max(gameSpeedMultiplier, 1f);

                foreach (var step in currentTeachingData.steps)
                {
                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    float scaledStepTime = step.time / speedMul;
                    float elapsedTime = Time.time - teachingStartTime;
                    float waitTime = scaledStepTime - elapsedTime;

                    if (waitTime > 0)
                    {
                        await Task.Delay((int)(waitTime * 1000), teachingCancellation.Token);
                    }

                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    WriteLog($"[GAME TEACHING] 리버스 [{i}/16] 스텝 {step.stepNumber}: {step.name}");
                    await ExecuteTeachingStep(step, speedMul);
                }

                if (teachingCancellation.Token.IsCancellationRequested) break;

                // 이 티칭 완료!
                lastCompletedTeachingNumber = i;
                WriteLog($"[GAME TEACHING] 리버스 [{i}/16] 완료 (누적 완료: {lastCompletedTeachingNumber}번)");

                // 다음 티칭 전 대기 (배속 적용)
                if (i < 16 && !stopAfterCurrentTeaching)
                {
                    int delayMs = (int)(1000 / speedMul);
                    await Task.Delay(delayMs, teachingCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                WriteLog($"[GAME TEACHING] 취소됨 ({i}번 진행 중)");
                break;
            }
            catch (Exception e)
            {
                WriteLog($"[GAME TEACHING] {fileName} 오류: {e.Message}, 건너뜀");
                continue;
            }
        }

        float totalElapsed = Time.time - allTeachingsStartTime;
        WriteLog($"[GAME TEACHING] 종료 (완료: {lastCompletedTeachingNumber}번까지, 총 {totalElapsed:F1}초)");

        // 마지막 퍼즐 배출: 밸브 OFF → 펌프 OFF + 1초 대기 + J2 올리기 (퍼즐 끌고가기 방지)
        WriteLog("[GAME TEACHING] 마지막 퍼즐 배출 - 밸브/펌프 OFF");
        await SendJsonRpc("set_do", "[{\"pin\": 0, \"value\": 0}]");  // 밸브 OFF (반전 후 블로우 방향)
        await SendJsonRpc("set_do", "[{\"pin\": 1, \"value\": 0}]");  // 펌프 OFF (밸브와 같이 토글)
        await Task.Delay(1000);  // 1초 대기

        // 현재 로봇 관절 위치 동기화 (슬라이더 값을 실제 위치로 맞춤)
        await GetCurrentJointPositionsAsync();

        // J2만 -90도로 올리기 (나머지 축은 현재 위치 유지)
        WriteLog("[GAME TEACHING] J2만 올리기 (-90도)");
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string liftParams = $"[{{\"pose\": {{\"kind\": 1, \"joint\": {{\"joint\": [{string.Format(culture, "{0:F6}, {1:F6}, {2:F6}, {3:F6}, {4:F6}, {5:F6}", sliders[0].value * Mathf.Deg2Rad, -90f * Mathf.Deg2Rad, sliders[2].value * Mathf.Deg2Rad, sliders[3].value * Mathf.Deg2Rad, sliders[4].value * Mathf.Deg2Rad, sliders[5].value * Mathf.Deg2Rad)}]}}}}), \"param\": {{\"velocity\": 5.00, \"acc\": 8.00, \"t\": 1.50}}}}]";
        await SendJsonRpc("move_joint", liftParams);
        await WaitUntilRobotIdle();

        // 게임 종료 시 진공펌프 OFF
        WriteLog("[GAME TEACHING] 진공펌프 OFF (게임 종료)");
        await SendJsonRpc("set_do", "[{\"pin\": 1, \"value\": 0}]");
        await SendJsonRpc("set_do", "[{\"pin\": 0, \"value\": 1}]");  // 밸브 OFF (모터 교체로 반전)

        isTeachingRunning = false;
        isGameModeTeaching = false;
        stopAfterCurrentTeaching = false;

        // 안전장치: 티칭이 하나도 완료되지 않았으면 콜백 호출하지 않음
        if (lastCompletedTeachingNumber > 0)
        {
            _onTeachingComplete?.Invoke();
        }
        else
        {
            WriteLog($"[GAME TEACHING] 경고: 완료된 티칭이 없음 - 콜백 호출 안 함");
        }
        _onTeachingComplete = null;
    }

    /// <summary>
    /// 게임 모드 리버스 정리 순차 실행 (내부)
    /// </summary>
    private async Task RunGameCleanupSequential(int count, System.Action onComplete, System.Action<int, int> onProgress = null)
    {
        isGameModeTeaching = true;
        teachingCancellation = new CancellationTokenSource();
        isTeachingRunning = true;
        allTeachingsStartTime = Time.time;

        // 정리중 UI - 게임 모드에서는 표시 안 함
        if (teachingActiveIndicator != null)
            teachingActiveIndicator.SetActive(true);

        WriteLog($"[GAME CLEANUP] 티칭 1~{count} 순차 실행 시작");

        // 정리 시작 시 펌프/밸브 모두 OFF로 초기화 (흡착 시점에만 같이 켜짐)
        WriteLog("[GAME CLEANUP] 펌프/밸브 OFF 초기화 (정리 시작)");
        await SendJsonRpc("set_do", "[{\"pin\": 0, \"value\": 0}]");  // 밸브 OFF (반전 후 블로우 방향)
        await SendJsonRpc("set_do", "[{\"pin\": 1, \"value\": 0}]");  // 펌프 OFF

        // 에디터: 프로젝트 루트, 빌드: exe 파일과 같은 폴더
        string baseDir = Application.isEditor
            ? Application.dataPath.Replace("/Assets", "")
            : Directory.GetParent(Application.dataPath).FullName;

        for (int i = 1; i <= count; i++)
        {
            if (teachingCancellation.Token.IsCancellationRequested) break;

            string fileName = $"robot_teaching_{i}.json";
            string filePath = Path.Combine(baseDir, fileName);

            if (!File.Exists(filePath))
            {
                WriteLog($"[GAME CLEANUP] 파일 없음: {fileName}, 건너뜀");
                continue;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                currentTeachingData = JsonUtility.FromJson<TeachingData>(json);

                if (currentTeachingData == null || currentTeachingData.steps == null || currentTeachingData.steps.Count == 0)
                {
                    WriteLog($"[GAME CLEANUP] 유효하지 않은 JSON: {fileName}, 건너뜀");
                    continue;
                }

                WriteLog($"[GAME CLEANUP] 티칭 [{i}/{count}] 시작: {currentTeachingData.name}");
                onProgress?.Invoke(i, count);

                float speedMul = Mathf.Max(gameSpeedMultiplier, 1f);
                teachingStartTime = Time.time;
                currentTeachingData.steps.Sort((a, b) => a.time.CompareTo(b.time));

                foreach (var step in currentTeachingData.steps)
                {
                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    float scaledStepTime = step.time / speedMul;
                    float elapsedTime = Time.time - teachingStartTime;
                    float waitTime = scaledStepTime - elapsedTime;

                    if (waitTime > 0)
                    {
                        await Task.Delay((int)(waitTime * 1000), teachingCancellation.Token);
                    }

                    if (teachingCancellation.Token.IsCancellationRequested) break;

                    WriteLog($"[GAME CLEANUP] 티칭 [{i}/{count}] 스텝 {step.stepNumber}: {step.name}");
                    await ExecuteTeachingStep(step, speedMul);
                }

                if (teachingCancellation.Token.IsCancellationRequested) break;

                WriteLog($"[GAME CLEANUP] 티칭 [{i}/{count}] 완료");

                // 다음 리버스 전 대기 (배속 적용)
                if (i < count)
                {
                    int delayMs = (int)(1000 / speedMul);
                    await Task.Delay(delayMs, teachingCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                WriteLog($"[GAME CLEANUP] 취소됨 (티칭 {i}번 진행 중)");
                break;
            }
            catch (Exception e)
            {
                WriteLog($"[GAME CLEANUP] {fileName} 오류: {e.Message}, 건너뜀");
                continue;
            }
        }

        float totalElapsed = Time.time - allTeachingsStartTime;
        WriteLog($"[GAME CLEANUP] 정리 완료 (총 {totalElapsed:F1}초)");

        // 정리 완료 시 진공펌프 OFF
        WriteLog("[GAME CLEANUP] 진공펌프 OFF (정리 완료)");
        await SendJsonRpc("set_do", "[{\"pin\": 1, \"value\": 0}]");
        await SendJsonRpc("set_do", "[{\"pin\": 0, \"value\": 1}]");  // 밸브 OFF (모터 교체로 반전)

        // 홈 포지션으로 복귀
        if (teachingStatusText != null)
            teachingStatusText.text = "홈 포지션 복귀 중...";

        isTeachingRunning = false;  // MoveToHomePosition의 isBusy 체크를 위해 먼저 해제
        await MoveToHomePosition();

        // 정리중 UI 숨기기
        if (teachingStatusText != null)
        {
            teachingStatusText.text = $"정리 완료! (총 {totalElapsed:F1}초)";
            await Task.Delay(2000);
            teachingStatusText.gameObject.SetActive(false);
        }
        if (teachingActiveIndicator != null)
            teachingActiveIndicator.SetActive(false);

        isGameModeTeaching = false;
        lastCompletedTeachingNumber = 0;  // 정리 완료 후 리셋

        onComplete?.Invoke();
    }

    #endregion
}
