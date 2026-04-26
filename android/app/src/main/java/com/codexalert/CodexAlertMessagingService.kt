package com.codexalert

import android.content.Intent
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import java.time.OffsetDateTime
import java.time.ZoneOffset

class CodexAlertMessagingService : FirebaseMessagingService() {
    override fun onMessageReceived(remoteMessage: RemoteMessage) {
        val data = remoteMessage.data.toMutableMap()
        if (!data.containsKey("receivedAtUtc")) {
            data["receivedAtUtc"] = OffsetDateTime.now(ZoneOffset.UTC).toString()
        }

        val message = AlertStore.createMessageFromPayload(
            data = data,
            notificationTitle = remoteMessage.notification?.title,
            notificationBody = remoteMessage.notification?.body
        )

        AlertStore(this).addMessage(message)
        NotificationHelper.showMessage(this, message)
        sendBroadcast(Intent(ACTION_MESSAGE_RECEIVED).setPackage(packageName))
    }

    override fun onNewToken(token: String) {
        AlertStore(this).saveFcmToken(token, needsPcConfigUpdate = true)
        sendBroadcast(Intent(ACTION_TOKEN_REFRESHED).setPackage(packageName))
    }

    companion object {
        const val ACTION_MESSAGE_RECEIVED = "com.codexalert.MESSAGE_RECEIVED"
        const val ACTION_TOKEN_REFRESHED = "com.codexalert.TOKEN_REFRESHED"
    }
}
