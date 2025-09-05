using System.Collections.Generic;

public interface IActionProvider
{
    // Add the actions this object exposes. Do not clear the list.
    void GetActions(PlayerContext ctx, List<ActionDesc> outActions);
}
