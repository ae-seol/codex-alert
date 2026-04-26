package com.codexalert

import org.json.JSONObject

data class AlertMessage(
    val id: String,
    val pcId: String,
    val pcName: String,
    val sourceAppId: String,
    val sourceAppName: String,
    val title: String,
    val body: String,
    val receivedAtUtc: String,
    val rawPayloadJson: String
) {
    fun toJson(): JSONObject = JSONObject()
        .put("id", id)
        .put("pcId", pcId)
        .put("pcName", pcName)
        .put("sourceAppId", sourceAppId)
        .put("sourceAppName", sourceAppName)
        .put("title", title)
        .put("body", body)
        .put("receivedAtUtc", receivedAtUtc)
        .put("rawPayloadJson", rawPayloadJson)

    companion object {
        fun fromJson(json: JSONObject): AlertMessage = AlertMessage(
            id = json.optString("id"),
            pcId = json.optString("pcId"),
            pcName = json.optString("pcName"),
            sourceAppId = json.optString("sourceAppId"),
            sourceAppName = json.optString("sourceAppName"),
            title = json.optString("title"),
            body = json.optString("body"),
            receivedAtUtc = json.optString("receivedAtUtc"),
            rawPayloadJson = json.optString("rawPayloadJson")
        )
    }
}
