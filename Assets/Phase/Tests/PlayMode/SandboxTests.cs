using System.Collections;
using System.IO;
using NUnit.Framework;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SandboxTests : InputTestFixture
{
    [UnityTest]
    public IEnumerator SceneKeyboardPixelFrameAndNoText()
    {
        yield return SceneManager.LoadSceneAsync("PhaseSandbox");
        var sandbox=Object.FindAnyObjectByType<PhaseSandbox>();Assert.That(sandbox,Is.Not.Null);
        yield return null;
        Assert.That(sandbox.Frame.width,Is.EqualTo(320));Assert.That(sandbox.Frame.height,Is.EqualTo(180));Assert.That(sandbox.Frame.filterMode,Is.EqualTo(FilterMode.Point));
        var k=InputSystem.AddDevice<Keyboard>();
        try
        {
            double before=sandbox.Controller.player.x;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
            yield return new WaitForSeconds(.25f);
            Assert.That(sandbox.Controller.player.x,Is.GreaterThan(before));
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.Z));
            yield return new WaitForSeconds(.05f);
            Assert.That(sandbox.Controller.phase,Is.EqualTo(1));Assert.That(sandbox.Controller.jumps,Is.EqualTo(1));
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.X,Key.UpArrow));
            yield return new WaitForSeconds(.2f);
            Assert.That(sandbox.Controller.dashes,Is.EqualTo(1));Assert.That(sandbox.Controller.phase,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None),Is.Empty);
            Assert.That(Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None),Is.Empty);
            var r=PhaseSandbox.Viewport(1000,600);Assert.That(r.width,Is.EqualTo(960));Assert.That(r.height,Is.EqualTo(540));
            LogAssert.NoUnexpectedReceived();
            string path=System.Environment.GetEnvironmentVariable("PHASE_CAPTURE_PATH");
            if(!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path,sandbox.Frame.EncodeToPNG());
                string screen=Path.ChangeExtension(path,"game.png");
                if(File.Exists(screen))File.Delete(screen);
                // Unity's Metal screenshot backend can emit depth warnings after gameplay checks.
                ScreenCapture.CaptureScreenshot(screen);
                for(int i=0;i<60&&!File.Exists(screen);i++)yield return null;
                Assert.That(File.Exists(screen),Is.True,"actual Game View capture");
                var rendered=new Texture2D(2,2);
                try
                {
                    rendered.LoadImage(File.ReadAllBytes(screen));
                    var viewport=PhaseSandbox.Viewport(rendered.width,rendered.height);
                    int scale=(int)viewport.width/320;
                    Color32 expected=sandbox.Frame.GetPixel(0,179);
                    for(int y=0;y<scale;y++)for(int x=0;x<scale;x++)
                    {
                        Color32 actual=rendered.GetPixel((int)viewport.x+x,rendered.height-1-(int)viewport.y-y);
                        Assert.That(actual.r,Is.EqualTo(expected.r).Within(2),"display red");
                        Assert.That(actual.g,Is.EqualTo(expected.g).Within(2),"display green");
                        Assert.That(actual.b,Is.EqualTo(expected.b).Within(2),"display blue");
                    }
                }
                finally { Object.Destroy(rendered); }
            }
        }
        finally { InputSystem.RemoveDevice(k); }
    }
}
