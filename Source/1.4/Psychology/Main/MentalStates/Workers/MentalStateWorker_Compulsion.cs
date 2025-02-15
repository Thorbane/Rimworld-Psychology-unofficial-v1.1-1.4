﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace Psychology
{
    public class MentalStateWorker_Compulsion : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            return !pawn.WorkTagIsDisabled(WorkTags.Cleaning) || !pawn.WorkTagIsDisabled(WorkTags.Hauling);
        }
    }
}
