// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Content.Goobstation.Common.SecretSalt;

namespace Content.Goobstation.Server.StationEvents.SecretPlus;

/// <summary>
/// Private author-only feedback report. Never include user IDs or IP addresses.
/// Normalize presentation characters, but do not censor the meaning of comments.
/// </summary>
public static class SecretSaltFeedbackReport
{
    public static string Format(int roundId, IReadOnlyList<SecretSaltRecordedFeedback> reviews)
    {
        var buffer = new StringBuilder();
        buffer.AppendLine($"🧂 Секрет Сіль 1.4 — відгуки за раунд #{roundId}");
        buffer.AppendLine($"Отримано відгуків: {reviews.Count}");

        if (reviews.Count == 0)
        {
            buffer.AppendLine("Гравці не залишили відгуків.");
            return buffer.ToString();
        }

        foreach (var entry in reviews.OrderBy(e => e.Nickname, StringComparer.OrdinalIgnoreCase))
        {
            var reasons = entry.Submission.Reasons.Count == 0
                ? "Не вказано"
                : string.Join(", ", entry.Submission.Reasons.Select(ReasonName));
            var comment = string.IsNullOrWhiteSpace(entry.Submission.Comment)
                ? "Без коментаря"
                : SafeText(entry.Submission.Comment, SecretSaltFeedbackValidator.MaxCommentLength);

            buffer.AppendLine();
            buffer.AppendLine($"Гравець: {SafeText(entry.Nickname, 80)}");
            buffer.AppendLine($"Оцінка: {entry.Submission.Rating}/5");
            buffer.AppendLine($"Причини: {reasons}");
            buffer.AppendLine($"Коментар: {comment}");
        }

        return buffer.ToString();
    }

    private static string ReasonName(SecretSaltFeedbackReason reason) => reason switch
    {
        SecretSaltFeedbackReason.TooFewAntagonists => "Замало антагоністів",
        SecretSaltFeedbackReason.TooManyAntagonists => "Забагато антагоністів",
        SecretSaltFeedbackReason.TooFewEvents => "Замало подій",
        SecretSaltFeedbackReason.TooChaotic => "Надмірний хаос",
        SecretSaltFeedbackReason.Other => "Інше",
        _ => "Невідома причина"
    };

    private static string SafeText(string value, int limit)
    {
        var result = new StringBuilder();
        foreach (var character in value)
        {
            if (result.Length >= limit)
                break;

            // Flatten line breaks to prevent report-header impersonation. Discord
            // disallows mentions separately with allowed_mentions.parse=[].
            if (char.IsControl(character))
            {
                if (character is '\r' or '\n' or '\t')
                    result.Append(' ');
                continue;
            }

            result.Append(character switch
            {
                '@' => '＠',
                '`' => '′',
                '|' => '¦',
                '<' => '‹',
                '>' => '›',
                '*' => '∗',
                '_' => '＿',
                _ => character
            });
        }

        return result.ToString();
    }
}
