// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.GameTicking.Rules;

namespace Content.Server._Pirate.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(SELFRuleSystem))]
public sealed partial class SELFRuleComponent : Component;
