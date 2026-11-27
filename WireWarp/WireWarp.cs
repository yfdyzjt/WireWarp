using Terraria;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;
using Terraria.ID;
using WireWarp.Frontend.tModLoader.IO;
using WireWarp.Frontend.Shared;

namespace WireWarp.Frontend.tModLoader;

internal sealed class WireWarp : Mod
{
    public override void Load()
    {
        Access.Instance = new Accessor();
        Runtime.DiagnosticSink = message => Logger.Info("WWTRACE " + message);
        On_Wiring.HitSwitch += OnHitSwitch;
    }

    public override void Unload()
    {
        On_Wiring.HitSwitch -= OnHitSwitch;
        Runtime.DiagnosticSink = null;
    }

    private static void OnHitSwitch(On_Wiring.orig_HitSwitch orig, int i, int j)
    {
        try
        {
            if (Runtime.TraceEnabled)
                Runtime.DiagnosticSink?.Invoke($"HitSwitch hook ({i},{j}): {Runtime.DescribeInput(i,j)}");
            Runtime.HitInput(i, j);
        }
        catch (Exception e) 
        {
            ModContent.GetInstance<WireWarp>().Logger.Error($"WireWarp hit switch failed: {e}");
        }
    }
}

internal sealed class WireWarpSystem : ModSystem
{
    private bool _reportedPump;
    public override void OnWorldLoad()
    {
        _reportedPump = false;
        CampfireMode.ResetWorld();
        try
        {
            Runtime.Startup();
        }
        catch (Exception e)
        {
            ModContent.GetInstance<WireWarp>().Logger.Error($"WireWarp startup failed: {e}");
            throw;
        }
    }

    // Drive the accelerator once per active game update, not per world simulation update.
    // Do not also register PostUpdateWorld: that would double-step the backend.
    public override void PostUpdateEverything()
    {
        if (!Runtime.IsOpen || Main.netMode == NetmodeID.MultiplayerClient ||
            (Main.gameMenu && Main.netMode != NetmodeID.Server))
            return;

        try
        {
            if (!_reportedPump)
            {
                _reportedPump = true;
                Runtime.DiagnosticSink?.Invoke("Runtime pump active: PostUpdateEverything");
            }
            Runtime.Tick();
            BenchmarkSession.Update();
        }
        catch (Exception e)
        {
            ModContent.GetInstance<WireWarp>().Logger.Error($"WireWarp tick failed: {e}");
        }
    }

    // This is a UI draw callback proxy, not a hardware presentation counter.
    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (Runtime.IsOpen && Runtime.IsRun) BenchmarkSession.Draw();
    }

    public override void OnWorldUnload()
    {
        try
        {
            Runtime.Shutdown();
        }
        catch (Exception e)
        {
            ModContent.GetInstance<WireWarp>().Logger.Error($"WireWarp shutdown failed: {e}");
        }
    }
}
