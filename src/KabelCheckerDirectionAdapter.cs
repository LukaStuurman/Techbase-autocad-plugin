namespace Enexis.KabelChecker.AutoCAD;

internal sealed partial class KabelCheckerForm
{
    internal DirectionState? CaptureSelectedDirectionInfo()
    {
        if (SelectedDirectionNumber == null && _editingDirectionNumber == null)
            throw new InvalidOperationException("Selecteer eerst een opgeslagen richting bij 'Opgeslagen', of sla de huidige richting op.");
        // Use the original save/validation workflow, so pending grid edits and
        // current category mappings are reflected in the placed information.
        if (!SaveDirectionCore(false)) return null;
        return _editingDirectionNumber is int number ? _store.Get(number) : null;
    }
}
