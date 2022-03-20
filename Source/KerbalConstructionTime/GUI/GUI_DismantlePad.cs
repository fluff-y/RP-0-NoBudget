﻿using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        public static void DrawDismantlePadWindow(int windowID)
        {
            LCItem activeLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            bool isLC = GUIStates.ShowDismantleLC;
            GUILayout.BeginVertical();
            GUILayout.Label("Are you sure you want to dismantle the currently selected " + 
                (isLC ? $"launch complex, {activeLC.Name}" : $"launch pad, {activeLC.ActiveLPInstance.name}") + "? This cannot be undone!");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Yes"))
            {
                if (isLC)
                {
                    if (!activeLC.IsPad)
                    {
                        ScreenMessages.PostScreenMessage("Dismantle failed: can't dismantle the Hangar", 5f, ScreenMessageStyle.UPPER_CENTER);
                        return;
                    }
                    if (!activeLC.CanModify)
                    {
                        ScreenMessages.PostScreenMessage("Dismantle failed: Launch Complex in use", 5f, ScreenMessageStyle.UPPER_CENTER);
                        return;
                    }
                    KSCItem ksc = activeLC.KSC;
                    ksc.SwitchToPrevLaunchComplex();

                    for (int i = activeLC.Warehouse.Count; i-- > 0;)
                    {
                        Utilities.ScrapVessel(activeLC.Warehouse[i]);
                    }
                    ksc.LaunchComplexes.Remove(activeLC);
                }
                else
                {
                    if (activeLC.LaunchPadCount < 2) return;

                    KCT_LaunchPad lpToDel = activeLC.ActiveLPInstance;
                    if (!lpToDel.Delete(out string err))
                    {
                        ScreenMessages.PostScreenMessage("Dismantle failed: " + err, 5f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowBuildList = true;
            }

            if (GUILayout.Button("No"))
            {
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowDismantlePad = false;
                GUIStates.ShowDismantleLC = false;
                GUIStates.ShowBuildList = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
