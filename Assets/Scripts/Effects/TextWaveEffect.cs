using UnityEngine;
using TMPro;

/// <summary>
/// TextMeshPro 텍스트에 웨이브/일렁임 효과
/// 글자가 위아래로 물결치듯 움직임
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextWaveEffect : MonoBehaviour
{
    [Header("웨이브 설정")]
    // 웨이브 높이 (위아래 움직임 크기)
    [SerializeField] private float _waveHeight = 5f;

    // 웨이브 속도
    [SerializeField] private float _waveSpeed = 2f;

    // 글자 간 웨이브 간격 (높을수록 물결이 넓음)
    [SerializeField] private float _waveFrequency = 0.5f;

    [Header("추가 효과")]
    // 좌우 흔들림 추가
    [SerializeField] private bool _useHorizontalWave = false;

    // 좌우 흔들림 크기
    [SerializeField] private float _horizontalAmount = 2f;

    private TextMeshProUGUI _textMesh;
    private Mesh _mesh;
    private Vector3[] _vertices;

    void Awake()
    {
        _textMesh = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        _textMesh.ForceMeshUpdate();
        _mesh = _textMesh.mesh;
        _vertices = _mesh.vertices;

        for (int i = 0; i < _textMesh.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = _textMesh.textInfo.characterInfo[i];

            if (!charInfo.isVisible)
                continue;

            int vertexIndex = charInfo.vertexIndex;

            // 각 글자별 웨이브 오프셋
            float waveOffset = i * _waveFrequency;
            float yOffset = Mathf.Sin(Time.time * _waveSpeed + waveOffset) * _waveHeight;
            float xOffset = 0f;

            if (_useHorizontalWave)
            {
                xOffset = Mathf.Cos(Time.time * _waveSpeed + waveOffset) * _horizontalAmount;
            }

            Vector3 offset = new Vector3(xOffset, yOffset, 0);

            // 4개의 버텍스에 오프셋 적용
            _vertices[vertexIndex + 0] += offset;
            _vertices[vertexIndex + 1] += offset;
            _vertices[vertexIndex + 2] += offset;
            _vertices[vertexIndex + 3] += offset;
        }

        _mesh.vertices = _vertices;
        _textMesh.canvasRenderer.SetMesh(_mesh);
    }

    /// <summary>
    /// 웨이브 강도 설정
    /// </summary>
    public void SetWaveHeight(float height)
    {
        _waveHeight = height;
    }

    /// <summary>
    /// 웨이브 속도 설정
    /// </summary>
    public void SetWaveSpeed(float speed)
    {
        _waveSpeed = speed;
    }
}
