[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\HaloAnimationState.cs"
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Missing source file: $sourcePath"
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$harness = @'
namespace TorchlightRekindled
{
    public static class HaloAnimationStateContractHarness
    {
        public static void Run()
        {
            TestOrdinaryBlock();
            TestPommelBlock();
            TestRapidReblock();
            TestLightParry();
        }

        private static void TestOrdinaryBlock()
        {
            HaloAnimationState state = new HaloAnimationState();
            AssertFrame(state.Update(0f, false, true, false), 1f, 1f, "block start");
            AssertFrame(state.Update(0.1f, false, true, false), 1f, 0.5f, "block fade midpoint");
            AssertFrame(state.Update(0.2f, false, true, false), 1f, 0f, "block hidden");
            AssertFrame(state.Update(0.3f, false, false, false), 1f, 0f, "ordinary recovery start");
            AssertFrame(state.Update(0.4f, false, false, false), 1f, 0.5f, "ordinary recovery midpoint");
            AssertFrame(state.Update(0.5f, false, false, false), 1f, 1f, "ordinary recovery complete");
        }

        private static void TestPommelBlock()
        {
            HaloAnimationState state = new HaloAnimationState();
            state.Update(1f, false, true, false);
            state.Update(1.2f, false, true, true);
            AssertFrame(state.Update(1.3f, false, true, false), 1f, 0f, "pommel returns to block");
            AssertFrame(state.Update(1.4f, false, false, false), 1f, 0f, "pommel recovery start");
            AssertFrame(state.Update(1.6f, false, false, false), 1f, 0.5f, "pommel recovery midpoint");
            AssertFrame(state.Update(1.8f, false, false, false), 1f, 1f, "pommel recovery complete");
        }

        private static void TestRapidReblock()
        {
            HaloAnimationState state = new HaloAnimationState();
            state.Update(2f, false, true, false);
            state.Update(2.2f, false, true, false);
            state.Update(2.3f, false, false, false);
            AssertFrame(state.Update(2.4f, false, false, false), 1f, 0.5f, "partial recovery");
            AssertFrame(state.Update(2.4f, false, true, false), 1f, 0.5f, "reblock remains continuous");
            AssertFrame(state.Update(2.5f, false, true, false), 1f, 0.25f, "reblock fade midpoint");
            AssertFrame(state.Update(2.6f, false, true, false), 1f, 0f, "reblock hidden");
        }

        private static void TestLightParry()
        {
            HaloAnimationState state = new HaloAnimationState();
            AssertFrame(state.Update(3f, true, false, false), 0.5f, 1f, "light parry active");
            AssertFrame(state.Update(3f, false, false, false), 0.5f, 1f, "light parry recovery start");
            AssertFrame(state.Update(3.15f, false, false, false), 0.75f, 1f, "light parry recovery midpoint");
            AssertFrame(state.Update(3.3f, false, false, false), 1f, 1f, "light parry recovery complete");
        }

        private static void AssertFrame(
            HaloAnimationFrame frame,
            float expectedScale,
            float expectedVisibility,
            string label)
        {
            AssertNear(frame.LightParryScaleMultiplier, expectedScale, label + " scale");
            AssertNear(frame.BlockVisibilityMultiplier, expectedVisibility, label + " visibility");
        }

        private static void AssertNear(float actual, float expected, string label)
        {
            if (System.Math.Abs(actual - expected) > 0.001f)
            {
                throw new System.InvalidOperationException(
                    label + ": expected " + expected + ", got " + actual + ".");
            }
        }
    }
}
'@

Add-Type -TypeDefinition ($source + [Environment]::NewLine + $harness) -Language CSharp
[TorchlightRekindled.HaloAnimationStateContractHarness]::Run()

Write-Host "Torchlight Rekindled halo animation contracts passed."
