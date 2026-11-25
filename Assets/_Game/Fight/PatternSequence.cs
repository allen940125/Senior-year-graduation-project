using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PatternSequence : AttackPatternBase
{
    [System.Serializable]
    public struct PatternStep
    {
        [Tooltip("要執行的攻擊模式")]
        public AttackPatternBase pattern;

        [Tooltip("執行此步驟前等待的秒數")]
        public float delayBefore;
    }

    [Header("序列設定")]
    [Tooltip("依序執行的攻擊步驟")]
    public List<PatternStep> steps = new List<PatternStep>();

    [ContextMenu("自動抓取子物件 Pattern")]
    public void AutoGetChildrenPatterns()
    {
        steps.Clear();
        var childPatterns = GetComponentsInChildren<AttackPatternBase>(true);
        foreach (var p in childPatterns)
        {
            if (p != this)
            {
                steps.Add(new PatternStep { pattern = p, delayBefore = 0f });
            }
        }
        Debug.Log($"已抓取 {steps.Count} 個 Pattern 步驟！");
    }

    protected override void OnExecute(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        transform.SetParent(null);
        StartCoroutine(RunSequenceRoutine(boss, speedMultiplier, isAngry));
    }

    private IEnumerator RunSequenceRoutine(BossStateMachine boss, float speedMultiplier, bool isAngry)
    {
        foreach (var step in steps)
        {
            if (step.pattern == null)
            {
                continue;
            }

            float waitTime = isAngry ? step.delayBefore * 0.8f : step.delayBefore;
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            bool completed = false;
            System.Action onFinished = () => completed = true;

            step.pattern.PatternFinished += onFinished;
            step.pattern.Execute(boss, speedMultiplier, isAngry);

            if (!step.pattern.IsFinished)
            {
                yield return new WaitUntil(() => completed);
            }

            step.pattern.PatternFinished -= onFinished;
        }

        FinishPattern();
    }

    [ContextMenu("測試發射序列")]
    public void DebugTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("請先按 Play！");
            return;
        }

        Execute(null, 1f, false);
    }

    [ContextMenu("生成全部步驟預覽")]
    public void GenerateAllPreviews()
    {
        GenerateEditorPreview();

        foreach (var step in steps)
        {
            if (step.pattern != null)
            {
                step.pattern.GenerateEditorPreview();
            }
        }
    }

    [ContextMenu("清除全部步驟預覽")]
    public void ClearAllPreviews()
    {
        ClearEditorPreview();

        foreach (var step in steps)
        {
            if (step.pattern != null)
            {
                step.pattern.ClearEditorPreview();
            }
        }
    }

    protected override void OnGeneratePreview(Transform previewContainer)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.pattern == null)
            {
                continue;
            }

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"Step {i}: {step.pattern.name} (delay {step.delayBefore}s)";
            marker.transform.SetParent(previewContainer);
            marker.transform.position = transform.position;
            marker.transform.localScale = Vector3.one * 0.4f;
            DestroyImmediate(marker.GetComponent<Collider>());
        }
    }
}