using SpiritOfTheSamuraiUltrawide;

int assertions = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    assertions++;
}

var subtitle = new object();
Check(ModLogic.ShouldFallbackContinue(subtitle, subtitle, 10, 20, true), "Unchanged waiting subtitle gets fallback");
Check(!ModLogic.ShouldFallbackContinue(subtitle, new object(), 10, 20, true), "Native advance after mod input cannot skip next line");
Check(!ModLogic.ShouldFallbackContinue(subtitle, subtitle, 20, 20, true), "Native advance before mod input cannot skip next line");
Check(!ModLogic.ShouldFallbackContinue(subtitle, subtitle, 10, 20, false), "Completed subtitle is not continued again");
Check(!ModLogic.ShouldFallbackContinue(null, null, 10, 20, true), "Missing subtitle never advances");

var repeat = new RepeatGate();
Check(repeat.Step(1, 0) == 1, "Initial press responds immediately");
Check(repeat.Step(1, 0.2f) == 0, "Initial delay prevents premature repeat");
Check(repeat.Step(1, 0.36f) == 1, "Held direction repeats after delay");
Check(repeat.Step(1, 0.4f) == 0, "Repeat interval enforced");
Check(repeat.Step(1, 30f) == 1 && repeat.Step(1, 30f) == 0, "Slow frame produces one step, no catch-up burst");
Check(repeat.Step(-1, 30.01f) == -1, "Reversing direction responds immediately");
Check(repeat.Step(0, 31) == 0 && repeat.Step(-1, 31.01f) == -1, "Release resets repeat delay");
var chord = new ChordGate();
Check(!chord.Step(false) && chord.Step(true) && !chord.Step(true), "Chord fires once while held");
Check(!chord.Step(false) && chord.Step(true), "Chord rearms after release");

float[] tops = { 0, 100, 250, 300 };
Check(ModLogic.FirstVisible(tops, -1) == 0, "Negative scroll begins at first row");
Check(ModLogic.FirstVisible(tops, 99) == 0, "Partially visible row retained");
Check(ModLogic.FirstVisible(tops, 100) == 1, "Exact row boundary excludes previous row");
Check(ModLogic.FirstVisible(tops, 299) == 2, "Last partial row found");
Check(ModLogic.FirstVisible(tops, 300) == 3, "Past-end index safely skips all rows");
Check(ModLogic.FirstVisible(new float[] { 0 }, 0) == 0, "Empty history is safe");
var random = new Random(13);
var many = new List<float> { 0 };
for (int i = 0; i < 2000; i++) many.Add(many[^1] + random.Next(20, 100));
for (int i = 0; i < 200; i++)
{
    float scroll = (float)random.NextDouble() * many[^1];
    int linear = 0;
    while (linear < 2000 && many[linear + 1] <= scroll) linear++;
    Check(ModLogic.FirstVisible(many, scroll) == linear, "Binary visibility search matches variable-height rows");
}

string testDir = Path.Combine(AppContext.BaseDirectory, "testdata", Guid.NewGuid().ToString("N"));
string path = Path.Combine(testDir, "history.json");
Check(HistoryStore.Read(path, 300).Lines.Count == 0, "Missing file is an empty history");
var document = new HistoryStore.Document();
document.Lines.Add(new HistoryStore.Line { Speaker = "武士", Text = "Hello\n\"world\"\t\\test", VoicePath = "Audio/voces/test", Volume = 0.4f, Pitch = 1.2f });
HistoryStore.Write(path, document);
var restored = HistoryStore.Read(path, 300);
Check(restored.Lines[0].Text == document.Lines[0].Text && restored.Lines[0].Speaker == "武士", "Unicode and escaped text round-trip");
Check(restored.Lines[0].VoicePath == "Audio/voces/test" && restored.Lines[0].Volume == 0.4f && restored.Lines[0].Pitch == 1.2f, "Replay metadata survives reload");
document.Lines.Add(new HistoryStore.Line { Text = "second" });
HistoryStore.Write(path, document);
Check(File.Exists(path + ".bak"), "Replacement retains recovery copy");
File.WriteAllText(path, "{broken");
Check(HistoryStore.Read(path, 300).Lines.Count == 1, "Corrupt primary recovers prior complete file");
HistoryStore.Write(path, new HistoryStore.Document(), true);
Check(!File.Exists(path + ".bak") && HistoryStore.Read(path, 300).Lines.Count == 0, "Clear removes saved text and recovery copy");
document.Lines.Clear();
for (int i = 0; i < 50; i++) document.Lines.Add(new HistoryStore.Line { Text = i.ToString() });
HistoryStore.Write(path, document, true);
restored = HistoryStore.Read(path, 10);
Check(restored.Lines.Count == 10 && restored.Lines[0].Text == "40", "Capacity preserves newest lines");
document.Lines.Add(new HistoryStore.Line { Text = " " });
document.Lines.Add(new HistoryStore.Line { Text = new string('x', 16385) });
document.Lines.Add(new HistoryStore.Line { Text = "valid", Volume = 4, Pitch = -12 });
HistoryStore.Write(path, document, true);
restored = HistoryStore.Read(path, 2000);
Check(restored.Lines.Count == 51 && restored.Lines[^1].Volume == 1 && restored.Lines[^1].Pitch == -3, "Invalid rows rejected and replay ranges clamped");
document.Version = 999;
HistoryStore.Write(path, document, true);
bool rejected = false;
try { HistoryStore.Read(path, 300); } catch (InvalidDataException) { rejected = true; }
Check(rejected, "Unknown format version fails safely");
Console.WriteLine($"Passed {assertions} assertions. Test data: {testDir}");
