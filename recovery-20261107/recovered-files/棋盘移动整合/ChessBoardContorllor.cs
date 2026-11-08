using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessBoardContorllor : MonoBehaviour
{
    public ChessBoard chessBoard;
    public ChessSlot currentChessSlot;//当前的棋盘格子
    public List<ChessSlot> chessSlots;
    public Coroutine moveCoroutine;
    public bool isMoving;
    public float speed;

    void Start()
    {
        if (chessBoard != null)
            chessSlots = chessBoard.chessSlots;
    }

    void Update()
    {

    }

    public void MoveSteps(int num)
    {
        if (isMoving || num <= 0) return;
        if (chessBoard == null || currentChessSlot == null || chessBoard.MaxNum <= 0 || speed <= 0)
        {
            Debug.LogWarning("请先设置棋盘、当前格子和大于 0 的移动速度。", this);
            return;
        }

        isMoving = true;
        moveCoroutine = StartCoroutine(MoveTo(num));
    }

    private IEnumerator MoveTo(int num)
    {
        int cycle = num / chessBoard.MaxNum;//要走的完整圈数
        int remaining = num % chessBoard.MaxNum;//走完圈数后剩下的步数

        for (int i = 0; i < cycle; i++)
        {
            yield return StartCoroutine(MoveOneRound());
        }

        if (remaining > 0)
        {
            yield return StartCoroutine(MoveRemaining(remaining));
        }

        isMoving = false;
        moveCoroutine = null;
        OnArrived();
    }

    private IEnumerator MoveOneRound()
    {
        //走一圈必须实际走满格子数量，不能只判断终点位置。
        for (int i = 0; i < chessBoard.MaxNum; i++)
        {
            yield return StartCoroutine(MoveOneSlot());
        }
    }

    private IEnumerator MoveRemaining(int num)
    {
        for (int i = 0; i < num; i++)
        {
            yield return StartCoroutine(MoveOneSlot());
        }
    }

    private IEnumerator MoveOneSlot()
    {
        //如果方向不正确，先旋转到目前格子的移动方向。
        while ((transform.forward - currentChessSlot.moveDir).magnitude >= 0.1f)
        {
            Vector3 dir = currentChessSlot.moveDir;
            Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, q, speed * Time.deltaTime);
            yield return null;
        }

        ChessSlot nextChessSlot = chessBoard.GetChessSlot(currentChessSlot);
        yield return StartCoroutine(MoveAStep(transform, transform.position, nextChessSlot.transform.position, speed));
    }

    private bool OnArrived()
    {
        return true;
    }

    private IEnumerator MoveAStep(Transform who, Vector3 OriginPosition, Vector3 EndPosition, float speed)
    {
        who.position = OriginPosition;

        while (Vector3.Distance(who.position, EndPosition) > 0.01f)
        {
            who.position = Vector3.MoveTowards(
                who.position,
                EndPosition,
                speed * Time.deltaTime
            );

            yield return null;
        }

        //到达后消除容差，再更新当前格子。
        who.position = EndPosition;
        currentChessSlot = chessBoard.GetChessSlot(currentChessSlot);
    }
}
