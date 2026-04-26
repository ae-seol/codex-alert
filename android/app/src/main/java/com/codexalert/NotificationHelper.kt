package com.codexalert

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.media.AudioAttributes
import android.os.Build
import android.provider.Settings
import android.util.Log

object NotificationHelper {
    const val CHANNEL_ID = "codex_alerts_v2"

    fun ensureChannel(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return

        val manager = context.getSystemService(NotificationManager::class.java)
        val existing = manager.getNotificationChannel(CHANNEL_ID)
        if (existing != null) return

        val channel = NotificationChannel(
            CHANNEL_ID,
            "Codex alerts",
            NotificationManager.IMPORTANCE_HIGH
        ).apply {
            description = "Notifications relayed from Windows Codex"
            enableVibration(true)
            vibrationPattern = longArrayOf(0, 220, 120, 220)
            enableLights(true)
            lightColor = Color.rgb(255, 176, 32)
            lockscreenVisibility = Notification.VISIBILITY_PUBLIC
            setSound(
                Settings.System.DEFAULT_NOTIFICATION_URI,
                AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_NOTIFICATION)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                    .build()
            )
        }
        manager.createNotificationChannel(channel)
    }

    fun canPostNotifications(context: Context): Boolean {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            return context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED
        }
        val manager = context.getSystemService(NotificationManager::class.java)
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) manager.areNotificationsEnabled() else true
    }

    fun settingsIntent(context: Context): Intent {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                .putExtra(Settings.EXTRA_APP_PACKAGE, context.packageName)
        } else {
            Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS)
                .setData(android.net.Uri.parse("package:${context.packageName}"))
        }.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    }

    fun showMessage(context: Context, message: AlertMessage) {
        if (!canPostNotifications(context)) {
            Log.w(TAG, "Notification blocked by app/system settings: ${message.id}")
            return
        }

        ensureChannel(context)
        val intent = Intent(context, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            context,
            message.id.hashCode(),
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            Notification.Builder(context, CHANNEL_ID)
        } else {
            Notification.Builder(context)
        }
            .setSmallIcon(R.drawable.ic_stat_codex_alert)
            .setContentTitle(message.title.ifBlank { "Codex" })
            .setContentText(message.body)
            .setStyle(Notification.BigTextStyle().bigText(message.body))
            .setContentIntent(pendingIntent)
            .setTicker(message.title.ifBlank { "Codex Alert" })
            .setSubText(message.pcName.ifBlank { "Codex Alert" })
            .setCategory(Notification.CATEGORY_MESSAGE)
            .setPriority(Notification.PRIORITY_HIGH)
            .setDefaults(Notification.DEFAULT_ALL)
            .setVisibility(Notification.VISIBILITY_PUBLIC)
            .setAutoCancel(true)
            .build()

        context.getSystemService(NotificationManager::class.java)
            .notify(message.id.hashCode(), notification)
        Log.i(TAG, "Notification posted: ${message.id}")
    }

    private const val TAG = "CodexAlertNotify"
}
