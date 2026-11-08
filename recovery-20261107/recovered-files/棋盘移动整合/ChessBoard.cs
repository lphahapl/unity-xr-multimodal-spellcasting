using NUnit.Framework;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ChessBoard : MonoBehaviour
{
    public List<ChessSlot> chessSlots;
    public ChessBoardContorllor contorllor;
    public int startIndex;
    public int MaxNum { get; set; }

    public void Awake()
    {
        //使用 Inspector 列表中的顺序，不再自动追加格子。
        MaxNum = chessSlots == null ? 0 : chessSlots.Count;
        if (MaxNum == 0)
        {
            Debug.LogWarning("请先在 Inspector 中设置棋盘格子列表。", this);
            return;
        }

        for (int i = 0; i < MaxNum; i++)
        {
            if (chessSlots[i] == null)
            {
                MaxNum = 0;
                Debug.LogWarning("棋盘格子列表中存在空引用。", this);
                return;
            }
            chessSlots[i].index = i;
            chessSlots[i].moveDir = chessSlots[i].transform.right;
        }

        if (startIndex < 0 || startIndex >= MaxNum)
            startIndex = 0;

        if (contorllor == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                contorllor = player.GetComponent<ChessBoardContorllor>();
        }

        if (contorllor != null)
        {
            contorllor.chessBoard = this;
            contorllor.chessSlots = chessSlots;
            contorllor.currentChessSlot = chessSlots[startIndex];
            contorllor.transform.position = chessSlots[startIndex].transform.position;
        }
    }

    void Start()
    {

    }

    void Update()
    {

    }

    public ChessSlot GetChessSlot(ChessSlot now)
    {
        return chessSlots[(now.index + 1) % MaxNum];
    }
}
