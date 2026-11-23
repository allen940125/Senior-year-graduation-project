using UnityEngine;
using System.Collections.Generic;

public class FlappyPipePattern : BulletSpawnerBase
{
    [Header("水管幾何設定（需與水管 Prefab 高度一致）")]
    [Tooltip("單根水管的總長度（建議設得比整個畫面高度還長，多的藏在畫面外）")]
    public float singlePipeHeight = 15f;

    [Header("生成位置與缺口設定")]
    [Tooltip("相對於 Boss firePosition 的生成偏移（例如 (10, 0) 從畫面右側生出）")]
    public Vector2 spawnOffset = new Vector2(10f, 0f);

    [Tooltip("玩家能穿過的缺口高度")]
    public float gapHeight = 3.0f;

    [Tooltip("缺口中心點允許上下隨機偏移的範圍（±值）")]
    public float gapRandomRangeY = 3.0f;

    [Header("飛行方向")]
    public Vector2 moveDirection = Vector2.left;

    protected override List<SpawnData> CalculateAllSpawnData()
    {
        List<SpawnData> list = new List<SpawnData>();

        Vector2 baseCenter = (Vector2)transform.position + spawnOffset;
        Vector2 dir = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector2.left;

        // 決定缺口中心 Y 座標
        float gapCenterY = baseCenter.y + Random.Range(-gapRandomRangeY, gapRandomRangeY);

        // 算出中心點到上/下水管中心的偏移距離
        float offsetFromGapCenter = (gapHeight * 0.5f) + (singlePipeHeight * 0.5f);

        // 1. 上水管
        list.Add(new SpawnData
        {
            position = new Vector2(baseCenter.x, gapCenterY + offsetFromGapCenter),
            direction = dir
        });

        // 2. 下水管
        list.Add(new SpawnData
        {
            position = new Vector2(baseCenter.x, gapCenterY - offsetFromGapCenter),
            direction = dir
        });

        return list;
    }

    protected override void OnGeneratePreview(Transform previewContainer)
    {
        var dataList = CalculateAllSpawnData();
        foreach (var data in dataList)
        {
            CreatePreviewDummy(previewContainer, data.position, data.direction);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + (Vector3)spawnOffset;
        float offsetFromGapCenter = (gapHeight * 0.5f) + (singlePipeHeight * 0.5f);

        // 黃框：缺口可能隨機出現的範圍
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(1f, gapRandomRangeY * 2f, 0f));

        // 紅框：假設缺口在正中間時，上下兩根水管的實際位置
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center + Vector3.up * offsetFromGapCenter, new Vector3(1f, singlePipeHeight, 0f));
        Gizmos.DrawWireCube(center + Vector3.down * offsetFromGapCenter, new Vector3(1f, singlePipeHeight, 0f));
    }
}