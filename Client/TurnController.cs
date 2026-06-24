using Raylib_cs;
using ArenaGame.Core;

namespace ArenaGame.Client;

// Drives the player's turn: action-menu selection, target picking, submission, and the
// Waiting → resolved transition. Owns the UI mode and what was submitted; on resolution it
// feeds the event log and hit splats.
sealed class TurnController
{
    enum Mode { Menu, Targeting, Waiting }

    readonly ServerConnection _conn;
    readonly PlayerView _view;
    readonly EventLog _log;
    readonly HitSplats _splats;

    Mode _mode = Mode.Menu;
    string? _selectedActionId;
    int? _submittedAtTick;
    Position? _pendingMoveTarget;   // a submitted move's destination, to detect "got blocked"

    public TurnController(ServerConnection conn, PlayerView view, EventLog log, HitSplats splats)
    {
        _conn = conn;
        _view = view;
        _log = log;
        _splats = splats;
    }

    public string? SelectedActionId => _selectedActionId;

    public string ModeHint => _mode switch
    {
        Mode.Targeting => "Select a highlighted tile  (Esc / right-click to cancel)",
        Mode.Waiting => "Waiting for turn to resolve...",
        _ => "Choose an action"
    };

    // Legal target tiles for the selected action, via the same Core rules the server validates with.
    public List<Position> CurrentTargets(ObservationDto obs)
    {
        if (_mode != Mode.Targeting || _selectedActionId is null) return new();
        var model = StateModel.FromObservation(obs);
        return Rules.LegalTargets(model, obs.Self.Id, _selectedActionId);
    }

    public void HandleMouse(ObservationDto obs, List<(Rectangle rect, ActionDef action)> menuHits)
    {
        // A new tick resolved: log it, splat the hits, then return control to the player.
        if (_mode == Mode.Waiting && _submittedAtTick is int t && obs.Tick > t)
        {
            ResolveTick(obs);
            _mode = Mode.Menu;
            _submittedAtTick = null;
        }
        if (_mode == Mode.Waiting) return; // ignore input while resolving

        // Cancel targeting with Esc or right-click.
        if (_mode == Mode.Targeting &&
            (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsMouseButtonPressed(MouseButton.Right)))
        {
            _mode = Mode.Menu;
            _selectedActionId = null;
            return;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var m = Raylib.GetMousePosition();

        foreach (var (rect, action) in menuHits)
        {
            if (!Raylib.CheckCollisionPointRec(m, rect)) continue;
            OnMenuClick(obs, action);
            return;
        }

        if (_mode == Mode.Targeting && _selectedActionId is not null)
        {
            int tx = (int)(m.X / Layout.TileSize), ty = (int)(m.Y / Layout.TileSize);
            if (CurrentTargets(obs).Contains(new Position(tx, ty)))
                CommitTarget(obs, tx, ty);
        }
    }

    void OnMenuClick(ObservationDto obs, ActionDef action)
    {
        if (_mode == Mode.Waiting) return;

        // Re-clicking the already-selected action cancels targeting.
        if (_mode == Mode.Targeting && action.Id == _selectedActionId)
        {
            _mode = Mode.Menu;
            _selectedActionId = null;
            return;
        }

        _selectedActionId = action.Id;
        if (action.TargetType == TargetTypes.None || action.TargetType == TargetTypes.Self)
            CommitImmediate(obs, action);
        else
            _mode = Mode.Targeting;
    }

    void CommitImmediate(ObservationDto obs, ActionDef action)
    {
        Target? target = action.TargetType == TargetTypes.Self ? new Target(Self: true) : null;
        Submit(obs, new Intention(obs.Self.Id, action.Id, target));
    }

    void CommitTarget(ObservationDto obs, int tx, int ty)
    {
        Submit(obs, new Intention(obs.Self.Id, _selectedActionId!, new Target(Position: new Position(tx, ty))));
    }

    void Submit(ObservationDto obs, Intention intention)
    {
        // Remember a move's destination so the next tick can tell if it was blocked.
        _pendingMoveTarget = intention.ActionId == ActionIds.Move ? intention.Target?.Position : null;

        // Fire the attack animation (even on a miss), turning to face the target tile.
        if (intention.ActionId == ActionIds.BasicAttack && intention.Target?.Position is Position tp)
            _view.StartAttack(obs.Self.Id, obs.Self.Position, tp);

        _submittedAtTick = obs.Tick;
        _mode = Mode.Waiting;
        _selectedActionId = null;
        _ = SendAndHandle(intention);
    }

    async Task SendAndHandle(Intention intention)
    {
        if (!await _conn.Send(intention))
        {
            // Rejected or transport failure: don't strand the player in Waiting.
            _mode = Mode.Menu;
            _submittedAtTick = null;
        }
    }

    // Turn the just-resolved tick into log lines and hit splats.
    void ResolveTick(ObservationDto obs)
    {
        if (_pendingMoveTarget is Position want &&
            (obs.Self.Position.X != want.X || obs.Self.Position.Y != want.Y))
            _log.Add("Your move was blocked!");
        _pendingMoveTarget = null;

        _log.RecordEvents(obs);
        _splats.RegisterFromEvents(obs);
    }
}
