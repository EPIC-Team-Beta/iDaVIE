/*
 * iDaVIE (immersive Data Visualisation Interactive Explorer)
 * Copyright (C) 2024 IDIA, INAF-OACT
 *
 * This file is part of the iDaVIE project.
 *
 * iDaVIE is free software: you can redistribute it and/or modify it under the terms
 * of the GNU Lesser General Public License (LGPL) as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version.
 *
 * iDaVIE is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR
 * PURPOSE. See the GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License along with
 * iDaVIE in the LICENSE file. If not, see <https://www.gnu.org/licenses/>.
 *
 * Additional information and disclaimers regarding liability and third-party
 * components can be found in the DISCLAIMER and NOTICE files included with this project.
 *
 */
using System;

namespace iDaVIE.Kernel.Contracts
{
    public enum PanelPlacement
    {
        LeftPane, RightPane, BottomPane, MenuBar, Floating
    }

    // ST1 declares, ST6 realizes (shared_interfaces.md §1.5). The shell host token passed
    // to onMount is a Unity Transform; presentation assemblies cast it themselves.
    public interface IDesktopShell
    {
        void RegisterPanel(string panelId, string title, PanelPlacement placement,
            Action<object> onMount, Action onUnmount);
        void UnregisterPanel(string panelId);
        void ShowPanel(string panelId);
        void HidePanel(string panelId);
        bool IsPanelVisible(string panelId);
        event Action<string> PanelShown;
        event Action<string> PanelHidden;
    }
}
