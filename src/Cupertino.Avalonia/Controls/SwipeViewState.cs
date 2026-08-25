namespace Cupertino.Controls;

/// <summary>
/// Describes the resting presentation state of a <see cref="CupertinoSwipeView"/>.
/// </summary>
public enum SwipeViewState
{
    /// <summary>
    /// No swipe actions are visible.
    /// </summary>
    Closed,

    /// <summary>
    /// The actions attached to the leading edge are visible.
    /// </summary>
    LeadingVisible,

    /// <summary>
    /// The actions attached to the trailing edge are visible.
    /// </summary>
    TrailingVisible,
}
