package com.codexalert

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log
import java.time.OffsetDateTime
import java.time.ZoneOffset

class DebugAlertReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val data = HashMap<String, String>()
        intent.extras?.keySet()?.forEach { key ->
            intent.extras?.get(key)?.let { value ->
                data[key] = value.toString()
            }
        }

        if (!data.containsKey("type")) {
            data["type"] = "codex_notification"
        }
        if (!data.containsKey("toastId")) {
            data["toastId"] = "debug-broadcast-${System.currentTimeMillis()}"
        }
        if (!data.containsKey("receivedAtUtc")) {
            data["receivedAtUtc"] = OffsetDateTime.now(ZoneOffset.UTC).toString()
        }
        if (!data.containsKey("sourceAppName")) {
            data["sourceAppName"] = "Codex"
        }

        val message = AlertStore.createMessageFromPayload(
            data = data,
            notificationTitle = data["title"],
            notificationBody = data["body"]
        )

        AlertStore(context).addMessage(message)
        NotificationHelper.showMessage(context, message)
        Log.i("CodexAlertDebug", "Debug alert received: ${message.id}")
    }
}
