package com.codexalert

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject

class AlertStore(context: Context) {
    private val prefs = context.applicationContext.getSharedPreferences("codex_alert_store", Context.MODE_PRIVATE)

    fun getMessages(): List<AlertMessage> {
        val raw = prefs.getString(KEY_MESSAGES, "[]") ?: "[]"
        val array = runCatching { JSONArray(raw) }.getOrDefault(JSONArray())
        val messages = ArrayList<AlertMessage>(array.length())
        for (index in 0 until array.length()) {
            val item = array.optJSONObject(index) ?: continue
            messages.add(AlertMessage.fromJson(item))
        }
        return messages
    }

    fun addMessage(message: AlertMessage) {
        val existing = getMessages().toMutableList()
        existing.removeAll { it.id == message.id }
        existing.add(0, message)
        val trimmed = existing.take(MAX_MESSAGES)
        val array = JSONArray()
        trimmed.forEach { array.put(it.toJson()) }
        prefs.edit().putString(KEY_MESSAGES, array.toString()).apply()
    }

    fun clearMessages() {
        prefs.edit().remove(KEY_MESSAGES).apply()
    }

    fun saveFcmToken(token: String, needsPcConfigUpdate: Boolean) {
        prefs.edit()
            .putString(KEY_FCM_TOKEN, token)
            .putBoolean(KEY_TOKEN_NEEDS_PC_CONFIG_UPDATE, needsPcConfigUpdate)
            .apply()
    }

    fun getFcmToken(): String = prefs.getString(KEY_FCM_TOKEN, "") ?: ""

    fun tokenNeedsPcConfigUpdate(): Boolean = prefs.getBoolean(KEY_TOKEN_NEEDS_PC_CONFIG_UPDATE, false)

    fun acknowledgeTokenCopied() {
        prefs.edit().putBoolean(KEY_TOKEN_NEEDS_PC_CONFIG_UPDATE, false).apply()
    }

    companion object {
        private const val KEY_MESSAGES = "messages"
        private const val KEY_FCM_TOKEN = "fcm_token"
        private const val KEY_TOKEN_NEEDS_PC_CONFIG_UPDATE = "token_needs_pc_config_update"
        private const val MAX_MESSAGES = 100

        fun createMessageFromPayload(data: Map<String, String>, notificationTitle: String?, notificationBody: String?): AlertMessage {
            val title = data["title"] ?: notificationTitle ?: "Codex"
            val body = data["body"] ?: notificationBody ?: ""
            val toastId = data["toastId"].orEmpty()
            val generatedId = if (toastId.isNotBlank()) toastId else "android-${System.currentTimeMillis()}"
            val raw = JSONObject()
            data.forEach { (key, value) -> raw.put(key, value) }

            return AlertMessage(
                id = generatedId,
                pcId = data["pcId"].orEmpty(),
                pcName = data["pcName"].orEmpty(),
                sourceAppId = data["sourceAppId"].orEmpty(),
                sourceAppName = data["sourceAppName"] ?: "Codex",
                title = title,
                body = body,
                receivedAtUtc = data["receivedAtUtc"].orEmpty(),
                rawPayloadJson = raw.toString()
            )
        }
    }
}
