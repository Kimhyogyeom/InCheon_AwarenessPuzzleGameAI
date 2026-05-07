using UnityEngine;
using System;

public class TimedPanelActivator : MonoBehaviour
{
    [Header("활성화할 패널")]
    public GameObject targetPanel;

    [Header("활성화 시작 시각 (24시간 형식)")]
    public int activateHour = 16;
    public int activateMinute = 50;

    [Header("활성화 종료 시각 (24시간 형식)")]
    public int deactivateHour = 17;
    public int deactivateMinute = 10;

    void Start()
    {
        if (targetPanel != null)
            targetPanel.SetActive(false);
    }

    void Update()
    {
        if (targetPanel == null) return;

        DateTime now = DateTime.Now;
        int nowMinutes = now.Hour * 60 + now.Minute;
        int startMinutes = activateHour * 60 + activateMinute;
        int endMinutes = deactivateHour * 60 + deactivateMinute;

        bool shouldBeActive = nowMinutes >= startMinutes && nowMinutes < endMinutes;

        if (shouldBeActive != targetPanel.activeSelf)
            targetPanel.SetActive(shouldBeActive);
    }
}
