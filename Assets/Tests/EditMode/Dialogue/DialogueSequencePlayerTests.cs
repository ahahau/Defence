using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Dialogue
{
    public class DialogueSequencePlayerTests
    {
        private static readonly Type SequenceType = Type.GetType("_01.Code.Dialogue.DialogueSequenceSO, Assembly-CSharp");
        private static readonly Type PlayerType = Type.GetType("_01.Code.Dialogue.DialogueSequencePlayer, Assembly-CSharp");
        private static readonly Type LineType = Type.GetType("_01.Code.Dialogue.DialogueLine, Assembly-CSharp");
        private static readonly Type ChoiceType = Type.GetType("_01.Code.Dialogue.DialogueChoice, Assembly-CSharp");

        [Test]
        public void Play_OutputsDisplayData_FromSequence()
        {
            var sequence = BuildSequence(("Speaker", "Hello", null));
            var player = BuildPlayer();

            var args = new object[] { sequence, null };
            var played = Invoke<bool>(player, "Play", args);
            var displayData = args[1];

            Assert.That(played, Is.True);
            Assert.That(GetProperty<string>(displayData, "SpeakerName"), Is.EqualTo("Speaker"));
            Assert.That(GetProperty<string>(displayData, "Text"), Is.EqualTo("Hello"));
            Assert.That(GetProperty<string>(displayData, "Progress"), Is.EqualTo("1/1"));

            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Next_DoesNotAdvance_WhenCurrentLineHasChoices()
        {
            var sequence = BuildSequence(("Speaker", "Pick one", new[] { ("Continue", 1) }), ("Speaker", "After", null));
            var player = BuildPlayer();

            Invoke<bool>(player, "Play", new object[] { sequence, null });
            var args = new object[] { null };
            var advanced = Invoke<bool>(player, "Next", args);

            Assert.That(advanced, Is.True);
            Assert.That(GetProperty<int>(player, "CurrentLineIndex"), Is.EqualTo(0));
            Assert.That(GetProperty<string>(args[0], "Text"), Is.EqualTo("Pick one"));

            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void SelectChoice_JumpsToExplicitNextLine()
        {
            var sequence = BuildSequence(
                ("Speaker", "Pick one", new[] { ("Skip", 2) }),
                ("Speaker", "Middle", null),
                ("Speaker", "Target", null));
            var player = BuildPlayer();

            Invoke<bool>(player, "Play", new object[] { sequence, null });
            Invoke<bool>(player, "SelectChoice", new object[] { 0, null, null, null, null });

            Assert.That(GetProperty<int>(player, "CurrentLineIndex"), Is.EqualTo(2));

            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void SelectChoice_Stops_WhenNextLineIndexIsEnd()
        {
            var sequence = BuildSequence(("Speaker", "Pick one", new[] { ("Continue", -1) }), ("Speaker", "After", null));
            var player = BuildPlayer();

            Invoke<bool>(player, "Play", new object[] { sequence, null });
            Invoke<bool>(player, "SelectChoice", new object[] { 0, null, null, null, null });

            Assert.That(GetProperty<bool>(player, "IsPlaying"), Is.False);
            Assert.That(GetProperty<int>(player, "CurrentLineIndex"), Is.EqualTo(-1));

            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void SelectChoice_Stops_WhenExplicitNextLineIsOutOfRange()
        {
            var sequence = BuildSequence(("Speaker", "Pick one", new[] { ("End", 99) }));
            var player = BuildPlayer();

            Invoke<bool>(player, "Play", new object[] { sequence, null });
            Invoke<bool>(player, "SelectChoice", new object[] { 0, null, null, null, null });

            Assert.That(GetProperty<bool>(player, "IsPlaying"), Is.False);
            Assert.That(GetProperty<int>(player, "CurrentLineIndex"), Is.EqualTo(-1));

            UnityEngine.Object.DestroyImmediate(sequence);
        }

        private static object BuildPlayer()
        {
            Assert.That(PlayerType, Is.Not.Null);
            return Activator.CreateInstance(PlayerType);
        }

        private static ScriptableObject BuildSequence(params (string speaker, string text, (string text, int nextLineIndex)[] choices)[] lines)
        {
            Assert.That(SequenceType, Is.Not.Null);
            Assert.That(LineType, Is.Not.Null);
            Assert.That(ChoiceType, Is.Not.Null);
            var sequence = ScriptableObject.CreateInstance(SequenceType);
            var lineArray = Array.CreateInstance(LineType, lines.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                var choices = lines[i].choices;
                var choiceArray = Array.CreateInstance(ChoiceType, choices?.Length ?? 0);
                for (var j = 0; j < choiceArray.Length; j++)
                    choiceArray.SetValue(BuildChoice(choices[j].text, choices[j].nextLineIndex), j);

                lineArray.SetValue(BuildLine(lines[i].speaker, lines[i].text, choiceArray), i);
            }

            Invoke<object>(sequence, "Configure", lineArray);
            return sequence;
        }

        private static object BuildLine(string speaker, string text, Array choices)
        {
            Assert.That(LineType, Is.Not.Null);
            var constructor = LineType.GetConstructor(new[] { typeof(string), typeof(string), ChoiceType.MakeArrayType() });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new object[] { speaker, text, choices });
        }

        private static object BuildChoice(string text, int nextLineIndex)
        {
            Assert.That(ChoiceType, Is.Not.Null);
            var constructor = ChoiceType.GetConstructor(new[] { typeof(string), typeof(int) });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new object[] { text, nextLineIndex });
        }

        private static T Invoke<T>(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            var result = method.Invoke(target, args);
            return result is T typed ? typed : default;
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }
    }
}
