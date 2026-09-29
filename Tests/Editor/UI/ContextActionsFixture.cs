using System;
using UnityEngine;

namespace ClarityConsole.Tests.UI
{
    /// <summary>
    /// A script with the kinds of <c>[ContextMenu]</c> methods a project writes for debugging, for
    /// <see cref="ContextActionsTests"/>. A ScriptableObject so the tests need no scene.
    /// </summary>
    internal class ContextActionsFixture : ScriptableObject
    {
        public int Gold;
        public bool CanSkip;

        [ContextMenu("Give 100 gold")]
        private void GiveGold()
        {
            Gold += 100;
        }

        [ContextMenu("Waves/Skip wave", false, 50)]
        public void SkipWave()
        {
        }

        [ContextMenu("Waves/Skip wave", true)]
        private bool CanSkipWave()
        {
            return CanSkip;
        }

        [ContextMenu("Break")]
        private void Break()
        {
            throw new InvalidOperationException("broken on purpose");
        }

        [ContextMenu("Reset first", false, -10)]
        private void ResetFirst()
        {
            Gold = 0;
        }

        // Not offered: the Inspector cannot call a method that takes arguments either.
        [ContextMenu("With argument")]
        private void WithArgument(int amount)
        {
            Gold += amount;
        }
    }
}
