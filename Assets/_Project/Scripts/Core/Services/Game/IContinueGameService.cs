using System;
using System.Collections;

public interface IContinueGameService
{
    bool CanContinue();
    bool ContinueGame();
    IEnumerator ContinueGameRoutine(Action<bool> completed);
}