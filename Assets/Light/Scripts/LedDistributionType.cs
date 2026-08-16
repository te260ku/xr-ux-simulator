public enum LedDistributionType
{
    Simultaneous,

    // 指定軸の座標が小さいLEDから大きいLEDへ
    LowToHigh,

    // 指定軸の座標が大きいLEDから小さいLEDへ
    HighToLow,

    // 中央から外側へ
    CenterOut,

    // 外側から中央へ
    OutsideIn
}