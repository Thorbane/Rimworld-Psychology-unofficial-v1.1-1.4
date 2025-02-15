﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System.Linq;
using Verse.AI;
using System.Reflection.Emit;

namespace Psychology
{
  public static class RomanceUtility
  {
    public static readonly HashSet<JobDef> jobDefsThatPreventHookups = new HashSet<JobDef> { JobDefOf.LayDown, JobDefOf.BeatFire, JobDefOf.Arrest, JobDefOf.Capture, JobDefOf.EscortPrisonerToBed, JobDefOf.ExtinguishSelf, JobDefOf.FleeAndCower, JobDefOf.MarryAdjacentPawn, JobDefOf.PrisonerExecution, JobDefOf.ReleasePrisoner, JobDefOf.Rescue, JobDefOf.SocialFight, JobDefOf.SpectateCeremony, JobDefOf.TakeToBedToOperate, JobDefOf.TakeWoundedPrisonerToBed, JobDefOf.UseCommsConsole, JobDefOf.Vomit, JobDefOf.Wait_Downed, JobDefOfPsychology.DoLovinCasual, JobDefOf.Lovin, JobDefOf.EnterTransporter, JobDefOf.GiveSpeech, JobDefOf.BestowingCeremony, JobDefOf.PrepareSkylantern, JobDefOf.Deathrest, JobDefOf.Breastfeed, JobDefOfPsychology.Abuse, JobDefOfPsychology.BreakHunt };

    public static SimpleCurve VanillaLovinMTBHoursAgeCurve => (SimpleCurve)AccessTools.Field(typeof(JobDriver_Lovin), "LovinIntervalHoursFromAgeCurve").GetValue(null);

    //pawn.CurJob.def != JobDefOfPsychology.JobDateLead &&
    //pawn.CurJob.def != JobDefOfPsychology.JobDateFollow &&
    //pawn.CurJob.def != JobDefOfPsychology.JobHangoutLead &&
    //pawn.CurJob.def != JobDefOfPsychology.JobHangoutFollow &&

    public static IEnumerable<CodeInstruction> InterdictRomanceAges(IEnumerable<CodeInstruction> codes, OpCode opCodePawn)
    {
      Log.Message("InterdictRomanceAges, start");
      foreach (CodeInstruction c in codes)
      {
        if (c.operand is float f && f == 16f)
        {
          Log.Message("InterdictRomanceAges, load opCodePawn");
          yield return new CodeInstruction(opCodePawn);
          Log.Message("InterdictRomanceAges, load Ldc_I4_0");
          yield return new CodeInstruction(OpCodes.Ldc_I4_0);
          Log.Message("InterdictRomanceAges, call SpeciesSettingsMinRomanceAge");
          yield return CodeInstruction.Call(typeof(RomanceUtility), nameof(RomanceUtility.SpeciesSettingsMinRomanceAge));
        }
        else
        {
          yield return c;
        }
      }
      Log.Message("InterdictRomanceAges, end");
    }

    public static float SpeciesSettingsMinRomanceAge(Pawn pawn, bool isDating)
    {
      float bioAge = pawn.ageTracker.AgeBiologicalYearsFloat;
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return bioAge + 1f;
      }
      SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
      float romanceAge = isDating ? settings.minDatingAge : settings.minLovinAge;
      return romanceAge < 0f ? bioAge + 1f : romanceAge;
    }

    public static float NewLovinMTBHoursScale(Pawn pawn, Pawn partner)
    {
      LovinMtbSinglePawnFactorPsychology(pawn, out float yMean1);
      LovinMtbSinglePawnFactorPsychology(partner, out float yMean2);
      return 0.5f * Mathf.Sqrt(yMean1 * yMean2) / 12f;
    }

    public static float LovinMtbSinglePawnFactorPsychology(Pawn pawn, out float yMean)
    {
      yMean = 0f;
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return -1f;
      }

      if (!SpeciesHelper.RomanceEnabled(pawn, false))
      {
        return -1f;
      }
      if (pawn?.ageTracker?.Adult != true)
      {
        //long AdultMinAgeTicks = pawn.ageTracker.AdultMinAgeTicks;
        //long AgeBiologicalTicks = pawn.ageTracker.AgeBiologicalTicks;
        //long TicksToAdulthood = AdultMinAgeTicks - AgeBiologicalTicks;
        //bool Adult = TicksToAdulthood <= 0f;
        //long AdultMinAgeTicks2 = partner.ageTracker.AdultMinAgeTicks;
        //long AgeBiologicalTicks2 = partner.ageTracker.AgeBiologicalTicks;
        //long TicksToAdulthood2 = AdultMinAgeTicks2 - AgeBiologicalTicks2;
        //bool Adult2 = TicksToAdulthood2 <= 0f;
        //Log.Message("GetLovinMtbHours, pawn: " + pawn + ", AdultMinAgeTicks: " + AdultMinAgeTicks + ", AgeBiologicalTicks: " + AgeBiologicalTicks + ", TicksToAdulthood: " + TicksToAdulthood + ", Adult: " + Adult + "\nGetLovinMtbHours, pawn2: " + partner + ", AdultMinAgeTicks2: " + AdultMinAgeTicks2 + ", AgeBiologicalTicks2: " + AgeBiologicalTicks2 + ", TicksToAdulthood2: " + TicksToAdulthood2 + ", Adult2: " + Adult2);

        // No underage lovin
        return -1f;
      }

      float num = 1f;
      num /= 1f - pawn.health.hediffSet.PainTotal;
      float level = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
      if (level < 0.5f)
      {
        num /= 2f * level;
      }

      if (ModsConfig.BiotechActive && pawn.genes != null)
      {
        foreach (Gene gene in pawn.genes.GenesListForReading)
        {
          num *= gene.def.lovinMTBFactor;
        }
      }

      float ageFactor = LovinMtbHoursAgeFactorAndMean(pawn, out yMean);
      if (ageFactor < 0f)
      {
        return -1f;
      }
      num *= ageFactor;

      return num;
    }

    public static float LovinMtbHoursAgeFactorAndMean(Pawn pawn, out float yMean)
    {
      yMean = 0f;
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return -1f;
      }

      SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
      if (settings.minLovinAge < 0f)
      {
        return -1f;
      }

      float lovinMTBHoursAgeFactor = 1f;
      if (settings.minLovinAge == 0f)
      {
        if (PsychologySettings.enableKinsey)
        {
          lovinMTBHoursAgeFactor /= Mathf.Pow(PsycheHelper.Comp(pawn).Sexuality.AdjustedSexDrive / 0.8f, 2f);
        }
        return lovinMTBHoursAgeFactor;
      }

      float ageCurveFactor = 1f;
      SimpleCurve rescaledCurve = GetRescaledCurve(pawn, out yMean, out bool useVanilla);
      float bioAge = pawn.ageTracker.AgeBiologicalYearsFloat;
      if (useVanilla)
      {
        float rescaledAge = PsycheHelper.LovinBioAgeToVanilla(bioAge, settings.minLovinAge);
        ageCurveFactor = rescaledCurve.Evaluate(bioAge);
      }
      else
      {
        ageCurveFactor = rescaledCurve.Evaluate(bioAge);
      }


      if (PsychologySettings.enableKinsey)
      {
        lovinMTBHoursAgeFactor *= Mathf.Sqrt(ageCurveFactor);
        lovinMTBHoursAgeFactor /= Mathf.Pow(PsycheHelper.Comp(pawn).Sexuality.AdjustedSexDrive / 0.8f, 1f);
      }
      else
      {
        lovinMTBHoursAgeFactor *= ageCurveFactor;
      }

      return lovinMTBHoursAgeFactor;
    }

    public static SimpleCurve GetRescaledCurve(Pawn pawn, out float yMean, out bool useVanilla)
    {
      SimpleCurve curve = GetCorrectAgeCurve(pawn, out useVanilla);
      RescaleCurve(curve, out yMean);
      return curve;
    }

    public static SimpleCurve GetCorrectAgeCurve(Pawn pawn, out bool useVanilla)
    {
      SimpleCurve curve = AlienRaceAgeCurveHook(pawn);
      useVanilla = curve == null;
      if (useVanilla)
      {
        curve = VanillaLovinMTBHoursAgeCurve;
      }
      return curve;
    }

    public static void RescaleCurve(SimpleCurve curve, out float yMean)
    {
      List<CurvePoint> oldCurvePointList = curve.Points;
      List<CurvePoint> newCurvePointList = new List<CurvePoint>();
      yMean = CalculateMeanOfCurve(oldCurvePointList);
      if (yMean == 0f)
      {
        return;
      }
      for (int i = 0; i < oldCurvePointList.Count() - 1; i++)
      {
        newCurvePointList.Add(new CurvePoint(oldCurvePointList[i].x, oldCurvePointList[i].y / yMean));
      }
      curve = new SimpleCurve(newCurvePointList);
    }

    public static float CalculateMeanOfCurve(List<CurvePoint> curvePoints)
    {
      if (curvePoints.NullOrEmpty())
      {
        Log.Warning("CalculateMeanOfCurve, curvePoints were null or empty");
        return 0f;
      }
      float dx;
      float xrange = 0f;
      float area = 0f;
      for (int i = 0; i < curvePoints.Count() - 2; i++)
      {
        dx = curvePoints[i + 1].x - curvePoints[i].x;
        xrange += dx;
        area += 0.5f * (curvePoints[i + 1].y + curvePoints[i].y) * dx;
      }
      float yMean = xrange > 0f ? area / xrange : curvePoints[0].y;
      return yMean;
    }

    public static SimpleCurve AlienRaceAgeCurveHook(Pawn pawn)
    {
      return null;
    }



    /// <summary>
    /// Builds a list of up to five other pawns that <paramref name="pawn"/> finds suitable for the given activity. Looks at romance chance factor for hookups and opinion for dates.
    /// </summary>
    /// <param name="pawn">The pawn who is looking</param>
    /// <param name="hookup">Whether this list is for a hookup or a date</param>
    /// <returns>A list of pawns, with love relations first, then descending order by the secondary factor.</returns>
    public static List<Pawn> FindAttractivePawns(Pawn pawn, bool hookup = true)
    {
      List<Pawn> result = new List<Pawn>();
      //Removed asexual check, it instead goes in the joy givers that generate jobs that need this
      //Put existing partners in the list
      if (LovePartnerRelationUtility.HasAnyLovePartner(pawn))
      {
        foreach (Pawn p in GetAllLoveRelationPawns(pawn, false, true))
        {
          //Skip pawns they share a bed with except for dates
          if (!DoWeShareABed(pawn, p) || !hookup)
          {
            result.Add(p);
          }
        }
      }
      //Stop here if non-spouse lovin' is not allowed
      if (!new HistoryEvent(HistoryEventDefOf.GotLovin_NonSpouse, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() && hookup)
      {
        return result;
      }
      //Then grab some attractive pawns
      while (result.Count < 5)
      {
        //For a hookup we need to start with a non-zero number, or pawns that they actually don't find attractive end up on the list
        //For a date we can start at zero since we're looking at opinion instead of romance factor
        float num = hookup ? 0.15f : 0f;
        Pawn tempPawn = null;
        foreach (Pawn p in pawn.Map.mapPawns.FreeColonistsSpawned)
        {
          //Skip them if they're already in the list, or they share a bed if it's a hookup
          if (result.Contains(p) || pawn == p || (DoWeShareABed(pawn, p) && hookup))
          {
            continue;
          }
          //For hookup check romance factor, for date check opinion
          else if ((pawn.relations.SecondaryRomanceChanceFactor(p) > num && hookup) || (pawn.relations.OpinionOf(p) > num && !hookup))
          {
            //This will skip people who recently turned them down
            Thought_Memory memory = pawn.needs.mood.thoughts.memories.Memories.Find(delegate (Thought_Memory x)
            {
              if (x.def == ThoughtDefOfPsychology.RebuffedMyHookupAttempt)
              {
                return x.otherPawn == p;
              }
              return false;
            });
            //Need to also check opinion against setting for a hookup
            if ((memory == null && pawn.relations.OpinionOf(p) > PsychologySettings.minOpinionForHookup) || !hookup)
            {
              num = pawn.relations.SecondaryRomanceChanceFactor(p);
              tempPawn = p;
            }

            //if ((memory == null && pawn.relations.OpinionOf(p) > pawn.MinOpinionForHookup()) || !hookup)
            //{
            //    num = pawn.relations.SecondaryRomanceChanceFactor(p);
            //    tempPawn = p;
            //}
          }
        }
        if (tempPawn == null)
        {
          break;
        }
        result.Add(tempPawn);
      }

      return result;
    }

    public static bool DoWeShareABed(Pawn pawn, Pawn other)
    {
      return pawn.ownership.OwnedBed != null && pawn.ownership.OwnedBed.OwnersForReading.Contains(other);
    }

    /// <summary>
    /// Determines if an interaction between <paramref name="pawn"/> and <paramref name="target"/> would be cheating from <paramref name="pawn"/>'s point of view. Includes a list of pawns that would think they are being cheated on.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="target"></param>
    /// <param name="cheaterList">A list of pawns who will think that <paramref name="pawn"/> cheated on them, regardless of what <paramref name="pawn"/> thinks</param>
    /// <returns>True or False</returns>
    public static bool IsThisCheating(Pawn pawn, Pawn target, out List<Pawn> cheaterList)
    {
      //This has to happen to get passed out
      cheaterList = new List<Pawn>();
      //Are they in a relationship?
      if (target != null && LovePartnerRelationUtility.LovePartnerRelationExists(pawn, target))
      {
        return false;
      }
      foreach (Pawn p in GetAllLoveRelationPawns(pawn, false, false))
      {
        //If the pawns have different ideos, I think this will check if the partner would feel cheated on per their ideo and settings
        //if (!new HistoryEvent(pawn.GetHistoryEventForLoveRelationCountPlusOne(), p.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() && p.CaresAboutCheating())
        //{
        //    cheaterList.Add(p);
        //}
        if (!new HistoryEvent(pawn.GetHistoryEventForLoveRelationCountPlusOne(), p.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() && p.CaresAboutCheating())
        {
          cheaterList.Add(p);
        }
      }
      //The cheater list is for use later, initiator will only look at their ideo and settings to decide if they're cheating
      if (new HistoryEvent(pawn.GetHistoryEventForLoveRelationCountPlusOne(), pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() || !pawn.CaresAboutCheating())
      {
        return false;
      }
      return true;
    }

    public static bool CaresAboutCheating(this Pawn pawn)
    {
      if (pawn?.story?.traits?.HasTrait(TraitDefOfPsychology.Polygamous) != false)
      {
        return false;
      }
      return true;
    }


    /// <summary>
    /// Returns true if interaction between <paramref name="pawn"/> and <paramref name="target"/> is not cheating and is allowed by ideo. Otherwise, finds the partner they would feel the worst about cheating on and decides based on opinion.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="target"></param>
    /// <param name="cheatOn">The pawn they feel worst about cheating on</param>
    /// <returns></returns>
    public static bool WillPawnContinue(Pawn pawn, Pawn target, out Pawn cheatOn)
    {
      cheatOn = null;
      if (IsThisCheating(pawn, target, out List<Pawn> cheatedOnList))
      {
        if (!cheatedOnList.NullOrEmpty())
        {
          //At this point, both the pawn and a non-zero number of partners consider this cheating
          //If they are faithful, don't do it
          //if (pawn.story.traits.HasTrait(RomanceDefOf.Faithful))
          //{
          //    return false;
          //}
          if (pawn.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
          {
            return false;
          }
          //Don't allow if user has turned cheating off
          if (PsychologySettings.hookupCheatChance == 0f)
          {
            return false;
          }
          //This should find the person they would feel the worst about cheating on
          //With the philanderer map differences, I think this is the best way
          float opinionFactor = 99999f;
          foreach (Pawn p in cheatedOnList)
          {
            float opinion = pawn.relations.OpinionOf(p);
            float tempOpinionFactor;
            //if (pawn.story.traits.HasTrait(RomanceDefOf.Philanderer))
            //{
            //    tempOpinionFactor = pawn.Map == p.Map
            //        ? Mathf.InverseLerp(70f, 15f, opinion)
            //        : Mathf.InverseLerp(100f, 50f, opinion);
            //}
            if (pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
            {
              tempOpinionFactor = pawn.Map == p.Map
                  ? Mathf.InverseLerp(70f, 15f, opinion)
                  : Mathf.InverseLerp(100f, 50f, opinion);
            }
            else
            {
              tempOpinionFactor = Mathf.InverseLerp(30f, -80f, opinion);
            }
            if (tempOpinionFactor < opinionFactor)
            {
              opinionFactor = tempOpinionFactor;
              cheatOn = p;
            }
          }
          if (Rand.Range(0f, 1f) * (PsychologySettings.hookupCheatChance / 100f) < opinionFactor)
          {
            return false;
          }
        }
        //Pawn thinks they are cheating, even though no partners will be upset
        //This can happen with the no spouses mod, which is a bit weird
        //Letting this continue for now, might change later
      }
      return true;
    }

    /// <summary>
    /// Determines if <paramref name="target"/> agrees to a hookup with <paramref name="asker"/>. Takes cheating into account.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="asker"></param>
    /// <returns>True or False</returns>
    public static bool IsHookupAppealing(Pawn target, Pawn asker)
    {
      if (!PsycheHelper.PsychologyEnabled(target) || !PsycheHelper.PsychologyEnabled(asker))
      {
        return false;
      }
      if (target.relations.OpinionOf(asker) < PsychologySettings.minOpinionForHookup)
      {
        return false;
      }
      //if (target.relations.OpinionOf(asker) < target.MinOpinionForHookup())
      //{
      //    return false;
      //}
      //Asexual pawns below a certain rating will only agree to sex with existing partners
      if (PsychologySettings.enableKinsey)
      {
        if (PsycheHelper.Comp(target).Sexuality.IsAsexual)
        {
          if (!LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
          {
            return false;
          }
        }
      }
      else if (target.story.traits.HasTrait(TraitDefOf.Asexual))
      {
        if (!LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
        {
          return false;
        }
        //Otherwise their rating is already factored in via secondary romance chance factor
      }


      //if (target.story.traits.HasTrait(TraitDefOf.Asexual) && target.AsexualRating() < 0.5f)
      //{
      //    if (!LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
      //    {
      //        return false;
      //    }
      //    //Otherwise their rating is already factored in via secondary romance chance factor
      //}
      if (WillPawnContinue(target, asker, out _))
      {
        //It's either not cheating or they have decided to cheat
        float romanceFactor = target.relations.SecondaryRomanceChanceFactor(asker);
        if (!LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
        {
          romanceFactor /= 1.5f;
        }
        float opinionFactor = 1f;
        //Decrease if opinion is negative
        opinionFactor *= Mathf.InverseLerp(-100f, 0f, target.relations.OpinionOf(asker));
        //Increase if opinion is positive, but on a lesser scale to above
        opinionFactor *= GenMath.LerpDouble(0, 100f, 1f, 1.5f, target.relations.OpinionOf(asker));
        return Rand.Range(0.05f, 1f) < (romanceFactor * opinionFactor);
      }
      return false;
    }

    /// <summary>
    /// Determines if <paramref name="target"/> accepts a date with <paramref name="asker"/>.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="asker"></param>
    /// <returns>True or false</returns>
    //public static bool IsDateAppealing(Pawn target, Pawn asker)
    //{
    //    //Always agree with an existing partner
    //    if (LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
    //    {
    //        return true;
    //    }
    //    if (WillPawnContinue(target, asker, out _))
    //    {
    //        //Definitely not cheating, or they decided to cheat
    //        //Same math as agreeing to a hookup but no asexual check
    //        float num = 0f;
    //        num += target.relations.SecondaryRomanceChanceFactor(asker) / 1.5f;
    //        num *= Mathf.InverseLerp(-100f, 0f, target.relations.OpinionOf(asker));
    //        return Rand.Range(0.05f, 1f) < num;
    //    }
    //    return false;
    //}

    //public static bool IsHangoutAppealing(Pawn target, Pawn asker)
    //{
    //    //Always agree with an existing partner?
    //    if (LovePartnerRelationUtility.LovePartnerRelationExists(target, asker))
    //    {
    //        return true;
    //    }
    //    //Just looking at opinion
    //    float num = Mathf.InverseLerp(-100f, 0f, target.relations.OpinionOf(asker));
    //    return Rand.Range(0.05f, 1f) < num;

    //}

    /// <summary>
    /// Will <paramref name="pawn"/> participate in a hookup. Checks settings and asexuality rating.
    /// </summary>
    /// <param name="pawn">The pawn in question</param>
    /// <returns>True or False</returns>
    public static bool WillPawnTryHookup(Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return false;
      }
      if (PsychologySettings.hookupRateMultiplier == 0f)
      {
        return false;
      }
      SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
      if (settings == null)
      {
        return false;
      }
      if (!SpeciesHelper.RomanceEnabled(pawn, false))
      {
        return false;
      }
      // ToDo: perhaps add a species specific enable hookup option


      //Sex repulsed asexual pawns will never agree to sex
      //if (pawn.story.traits.HasTrait(TraitDefOf.Asexual) && pawn.AsexualRating() < 0.2f)
      //{
      //    return false;
      //}
      //Is the race/pawnkind allowed to have hookups?
      //if (!pawn.HookupAllowed())
      //{
      //    return false;
      //}
      if (PsychologySettings.enableKinsey)
      {
        if (PsycheHelper.Comp(pawn).Sexuality.IsAsexual)
        {
          return false;
        }
      }
      else if (pawn.story.traits.HasTrait(TraitDefOf.Asexual))
      {
        return false;
      }
      //If their ideo prohibits all lovin', do not allow
      if (!new HistoryEvent(HistoryEventDefOf.SharedBed, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Returns false if <paramref name="pawn"/> is close to needing to eat/sleep, there's enemies nearby, they're drafted, or they're doing a job that should not be interrupted
    /// </summary>
    /// <param name="pawn"></param>
    /// <returns>True or False</returns>
    public static bool IsPawnFree(Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return false;
      }
      if (PawnUtility.WillSoonHaveBasicNeed(pawn) || PawnUtility.EnemiesAreNearby(pawn) || pawn.Drafted)
      {
        return false;
      }
      if (pawn?.health?.hediffSet?.HasHediff(HediffDefOf.PregnancyLabor) != false || pawn?.health?.hediffSet?.HasHediff(HediffDefOf.PregnancyLaborPushing) != false)
      {
        return false;
      }
      if (pawn?.CurJob is Job job)
      {
        if (job?.playerForced == true)
        {
          return false;
        }
        if (pawn?.jobs?.IsCurrentJobPlayerInterruptible() == false)
        {
          return false;
        }
        if (job?.def is JobDef def && jobDefsThatPreventHookups.Contains(def))
        {
          return false;
        }
      }
      return true;


    }

    /// <summary>
    /// Grabs the first non-spouse love partner of the opposite gender. For use in generating parents.
    /// </summary>
    /// <param name="pawn"></param>
    /// <returns></returns>
    public static Pawn GetFirstLoverOfOppositeGender(Pawn pawn)
    {
      foreach (Pawn lover in GetNonSpouseLovers(pawn, true))
      {
        if (pawn.gender.Opposite() == lover.gender)
        {
          return lover;
        }
      }
      return null;
    }

    /// <summary>
    /// Generates a list of love partners that does not include spouses or fiances
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="includeDead"></param>
    /// <returns></returns>
    public static List<Pawn> GetNonSpouseLovers(Pawn pawn, bool includeDead)
    {
      List<Pawn> list = new List<Pawn>();
      if (!pawn.RaceProps.IsFlesh)
      {
        return list;
      }
      List<DirectPawnRelation> relations = pawn.relations.DirectRelations;
      //List<DirectPawnRelation> relations = pawn.GetLoveRelations(includeDead);

      foreach (DirectPawnRelation rel in relations)
      {
        if ((rel.def == PawnRelationDefOf.Lover || rel.def == PawnRelationDefOf.Fiance) && (includeDead || !rel.otherPawn.Dead))
        {
          list.Add(rel.otherPawn);
        }
        //else if (SettingsUtilities.LoveRelations.Contains(rel.def) && (includeDead || !rel.otherPawn.Dead))
        //{
        //    list.Add(rel.otherPawn);
        //}
      }
      return list;
    }

    /// <summary>
    /// Generates a list of pawns that are in love relations with <paramref name="pawn"/>. Pawns are only listed once, even if they are in more than one love relation.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="includeDead">Whether dead pawns are added to the list</param>
    /// <param name="onMap">Whether pawns must be on the same map to be added to the list</param>
    /// <returns>A list of pawns that in a love relation with <paramref name="pawn"/></returns>
    public static List<Pawn> GetAllLoveRelationPawns(Pawn pawn, bool includeDead, bool onMap)
    {
      List<Pawn> list = new List<Pawn>();
      if (!pawn.RaceProps.IsFlesh)
      {
        return list;
      }
      foreach (DirectPawnRelation rel in LovePartnerRelationUtility.ExistingLovePartners(pawn, includeDead))
      {
        if (!list.Contains(rel.otherPawn))
        {
          if (pawn.Map == rel.otherPawn.Map)
          {
            list.Add(rel.otherPawn);
          }
          else if (!onMap)
          {
            list.Add(rel.otherPawn);
          }
        }
      }
      return list;
    }

    /// <summary>
    /// Finds the most liked pawn with a specific <paramref name="relation"/> to <paramref name="pawn"/>. Any direct relation will work, no implied relations.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="relation"></param>
    /// <param name="allowDead"></param>
    /// <returns></returns>
    public static Pawn GetMostLikedOfRel(Pawn pawn, PawnRelationDef relation, bool allowDead)
    {
      List<DirectPawnRelation> list = pawn.relations.DirectRelations;
      Pawn result = null;
      int num = 0;
      foreach (DirectPawnRelation rel in list)
      {
        if (rel.def == relation && (rel.otherPawn.Dead || allowDead))
        {
          if (pawn.relations.OpinionOf(rel.otherPawn) > num)
          {
            num = pawn.relations.OpinionOf(rel.otherPawn);
            result = rel.otherPawn;
          }
        }
      }
      return result;
    }

    public static bool HookupAllowed(this Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return false;
      }
      if (!SpeciesHelper.RomanceEnabled(pawn, false))
      {
        return false;
      }
      // ToDo: account for genes
      //if (pawn.genes != null)
      //{
      //    pawn.genes.HasGene(GeneDefOf.)
      //}
      return true;
    }


    /// <summary>
    /// A rating to use for determining sex aversion for asexual pawns. Seed is based on pawn's ID, so it will always return the same number for a given pawn.
    /// </summary>
    /// <param name="pawn"></param>
    /// <returns>float between 0 and 1</returns>
    //public static float AsexualRating(this Pawn pawn)
    //{
    //    Rand.PushState();
    //    Rand.Seed = pawn.thingIDNumber;
    //    float rating = Rand.Range(0f, 1f);
    //    Rand.PopState();
    //    return rating;
    //}

    /// <summary>
    /// Generates points for the lovin age curve based on age settings
    /// </summary>
    /// <returns>List<CurvePoint></returns>
    //public static List<CurvePoint> GetLovinCurve(this Pawn pawn)
    //{
    //    float minAge = pawn.MinAgeForSex();
    //    float maxAge = pawn.MaxAgeForSex();
    //    float declineAge = pawn.DeclineAtAge();
    //    List<CurvePoint> curves = new List<CurvePoint>
    //    {
    //        new CurvePoint(minAge, 1.5f),
    //        new CurvePoint((declineAge / 5) + minAge, 1.5f),
    //        new CurvePoint(declineAge, 4f),
    //        new CurvePoint((maxAge / 4) + declineAge, 12f),
    //        new CurvePoint(maxAge, 36f)
    //    };
    //    return curves;
    //}
  }
}