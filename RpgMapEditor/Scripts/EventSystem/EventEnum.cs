using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// メッセージウィンドウの位置
    /// </summary>
    public enum MessageWindowPosition
    {
        Top,
        Middle,
        Bottom
    }
    /// <summary>
    /// 方向
    /// </summary>
    public enum Direction
    {
        North,
        South,
        East,
        West,
        NorthEast,
        NorthWest,
        SouthEast,
        SouthWest
    }

    public enum FadeType
    {
        FadeIn,
        FadeOut
    }

    public enum EventTargetType
    {
        ThisEvent,
        EventID,
        EventName,
        Player
    }

    public enum LocationType
    {
        Direct,
        Variable,
        Exchange
    }

    public enum EventExchangeType
    {
        None,
        WithAnother,
        WithPlayer
    }

    public enum AnimationTargetType
    {
        Player,
        ThisEvent,
        Character,
        ScreenCenter
    }
    /// <summary>
    /// タイルコリジョンタイプ
    /// </summary>
    public enum TileCollisionType
    {
        None,
        Block,
        Half,
        Event,
        Damage,
        Slip
    }
    /// <summary>
    /// レイヤータイプ
    /// </summary>
    public enum LayerType
    {
        Background,
        Collision,
        Decoration,
        Overlay,
        Event
    }
}