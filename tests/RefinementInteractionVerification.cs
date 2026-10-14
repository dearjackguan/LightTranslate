using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LightTranslate
{
    internal static class RefinementInteractionVerification
    {
        internal static void Run()
        {
            using (var bitmap = new Bitmap(320, 200))
            using (var selection = new SelectionForm(bitmap, new Rectangle(0, 0, 320, 200)))
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(SelectionForm).GetMethod("OnMouseDown", flags).Invoke(selection,
                    new object[] { new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0) });
                bool before = (bool)typeof(SelectionForm).GetField("dragging", flags).GetValue(selection);
                selection.Capture = false;
                bool after = (bool)typeof(SelectionForm).GetField("dragging", flags).GetValue(selection);
                File.WriteAllText(@"A:\Agent\Codex\Translation\refinement\capture-loss-results.json",
                    new JavaScriptSerializer().Serialize(new { beforeDragging = before, afterDragging = after,
                        captureReleased = !selection.Capture }), Encoding.UTF8);
                ServiceVerification.Assert(before && !after, "Mouse capture loss leaves a stale selection drag.");
                typeof(SelectionForm).GetMethod("OnMouseDown", flags).Invoke(selection,
                    new object[] { new MouseEventArgs(MouseButtons.Left, 1, 20, 25, 0) });
                typeof(SelectionForm).GetMethod("OnMouseUp", flags).Invoke(selection,
                    new object[] { new MouseEventArgs(MouseButtons.Left, 1, 200, 100, 0) });
                ServiceVerification.Assert(selection.SelectedRegion == new Rectangle(20, 25, 180, 75),
                    "Selection did not recover after capture loss.");
            }
            Console.WriteLine("PASS selection capture-loss reset");
        }
    }
}
