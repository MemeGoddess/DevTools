using System;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace DevTools.Testing;

[PublicAPI]
public class WaitUntilTimeout : CustomYieldInstruction
{
  private readonly Func<bool> predicate;
  private readonly TickManager tickManager;
  private readonly int endTick;

  public WaitUntilTimeout(Func<bool> predicate, int maxTicksToWait = GenDate.TicksPerDay)
  {
    this.predicate = predicate;
    tickManager = Find.TickManager;
    endTick = tickManager.TicksGame + maxTicksToWait;
  }

  public override bool keepWaiting
  {
    get
    {
      if (tickManager.TicksGame > endTick)
      {
        Test.Fail("Timed out");
        return false;
      }
      return !predicate();
    }
  }
}