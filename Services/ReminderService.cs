using System;
using Microsoft.Toolkit.Uwp.Notifications;
using DesktopNotes.Models;

namespace DesktopNotes.Services
{
    public static class ReminderService
    {
        /// <summary>
        /// Schedules a Windows toast notification for the given note based on its ReminderAt property.
        /// Cancels any existing reminder for this note first.
        /// </summary>
        public static void ScheduleReminder(Note note)
        {
            if (note.ReminderAt == null)
            {
                CancelReminder(note);
                return;
            }

            // Cancel any previously scheduled toast for this note
            CancelReminder(note);

            if (note.ReminderAt.Value <= DateTime.Now)
            {
                return;
            }

            // Generate a unique tag for this reminder instance
            note.ReminderToastTag = Guid.NewGuid().ToString("N");

            try
            {
                var reminderText = string.IsNullOrWhiteSpace(note.ReminderText) ? note.Title : note.ReminderText;

                // Build the toast content
                var content = new ToastContentBuilder()
                    .AddText("PermaNotes Reminder")
                    .AddText(reminderText)
                    .AddButton(new ToastButton("Open Note", $"action=open&noteId={note.Id}"))
                    .GetToastContent();

                // Create and schedule the notification using ToastNotificationManagerCompat for unpackaged app support
                var scheduledToast = new Windows.UI.Notifications.ScheduledToastNotification(content.GetXml(), note.ReminderAt.Value)
                {
                    Tag = note.ReminderToastTag,
                    Group = "PermaNotesReminders"
                };

                ToastNotificationManagerCompat.CreateToastNotifier().AddToSchedule(scheduledToast);
                App.Trace($"ScheduleReminder: Successfully scheduled toast for note {note.Id} at {note.ReminderAt.Value:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                App.Trace($"ScheduleReminder: Failed to schedule toast notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancels any scheduled toast notification associated with this note.
        /// </summary>
        public static void CancelReminder(Note note)
        {
            if (string.IsNullOrEmpty(note.ReminderToastTag)) return;

            try
            {
                var notifier = ToastNotificationManagerCompat.CreateToastNotifier();
                var scheduledToasts = notifier.GetScheduledToastNotifications();
                
                foreach (var toast in scheduledToasts)
                {
                    if (toast.Tag == note.ReminderToastTag && toast.Group == "PermaNotesReminders")
                    {
                        try
                        {
                            notifier.RemoveFromSchedule(toast);
                        }
                        catch { }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Trace($"CancelReminder: Failed to cancel toast: {ex.Message}");
            }

            note.ReminderToastTag = string.Empty;
        }
    }
}
