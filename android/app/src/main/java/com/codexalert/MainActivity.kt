package com.codexalert

import android.Manifest
import android.app.Activity
import android.content.BroadcastReceiver
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.graphics.Color
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.widget.Button
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import com.google.firebase.messaging.FirebaseMessaging

class MainActivity : Activity() {
    private lateinit var store: AlertStore
    private lateinit var tokenView: TextView
    private lateinit var statusView: TextView
    private lateinit var inboxLayout: LinearLayout

    private val updatesReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            refreshTokenUi()
            renderInbox()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        store = AlertStore(this)
        NotificationHelper.ensureChannel(this)
        buildUi()
        consumeIntent(intent)
        requestNotificationPermissionIfNeeded()
        loadFcmToken()
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        consumeIntent(intent)
        renderInbox()
    }

    override fun onResume() {
        super.onResume()
        val filter = IntentFilter().apply {
            addAction(CodexAlertMessagingService.ACTION_MESSAGE_RECEIVED)
            addAction(CodexAlertMessagingService.ACTION_TOKEN_REFRESHED)
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            registerReceiver(updatesReceiver, filter, RECEIVER_NOT_EXPORTED)
        } else {
            registerReceiver(updatesReceiver, filter)
        }
        refreshTokenUi()
        renderInbox()
    }

    override fun onPause() {
        super.onPause()
        runCatching { unregisterReceiver(updatesReceiver) }
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == REQUEST_NOTIFICATIONS) {
            refreshTokenUi()
        }
    }

    private fun buildUi() {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(Color.rgb(247, 247, 242))
            setPadding(dp(20), dp(18), dp(20), dp(12))
        }

        val title = TextView(this).apply {
            text = "Codex Alert"
            textSize = 28f
            setTextColor(Color.rgb(26, 28, 33))
            typeface = android.graphics.Typeface.DEFAULT_BOLD
        }
        root.addView(title)

        statusView = TextView(this).apply {
            textSize = 14f
            setTextColor(Color.rgb(88, 92, 99))
            setPadding(0, dp(4), 0, dp(12))
        }
        root.addView(statusView)

        tokenView = TextView(this).apply {
            textSize = 13f
            setTextColor(Color.rgb(26, 28, 33))
            setBackgroundColor(Color.rgb(235, 237, 230))
            setPadding(dp(12), dp(10), dp(12), dp(10))
        }
        root.addView(tokenView, LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            LinearLayout.LayoutParams.WRAP_CONTENT
        ))

        val actions = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.END
            setPadding(0, dp(10), 0, dp(18))
        }
        actions.addView(makeButton("Copy token") {
            copyToken()
        })
        actions.addView(makeButton("Refresh") {
            loadFcmToken()
        })
        actions.addView(makeButton("Clear") {
            store.clearMessages()
            renderInbox()
        })
        actions.addView(makeButton("Settings") {
            startActivity(NotificationHelper.settingsIntent(this))
        })
        root.addView(actions)

        val inboxTitle = TextView(this).apply {
            text = "Inbox"
            textSize = 18f
            setTextColor(Color.rgb(26, 28, 33))
            typeface = android.graphics.Typeface.DEFAULT_BOLD
            setPadding(0, 0, 0, dp(8))
        }
        root.addView(inboxTitle)

        val scroll = ScrollView(this)
        inboxLayout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        scroll.addView(inboxLayout)
        root.addView(scroll, LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            0,
            1f
        ))

        setContentView(root)
    }

    private fun makeButton(label: String, onClick: () -> Unit): Button {
        return Button(this).apply {
            text = label
            textSize = 12f
            setAllCaps(false)
            setOnClickListener { onClick() }
        }
    }

    private fun requestNotificationPermissionIfNeeded() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) {
            requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), REQUEST_NOTIFICATIONS)
        }
    }

    private fun loadFcmToken() {
        runCatching {
            FirebaseMessaging.getInstance().token
                .addOnSuccessListener { token ->
                    val oldToken = store.getFcmToken()
                    store.saveFcmToken(token, needsPcConfigUpdate = oldToken.isNotBlank() && oldToken != token)
                    refreshTokenUi()
                }
                .addOnFailureListener {
                    tokenView.text = "FCM token unavailable: ${it.message}"
                }
        }.onFailure {
            tokenView.text = "FCM token unavailable. Add android/app/google-services.json for push testing."
        }
    }

    private fun consumeIntent(intent: Intent?) {
        val extras = intent?.extras ?: return
        val data = HashMap<String, String>()
        extras.keySet().forEach { key ->
            val value = extras.get(key)
            if (value != null) {
                data[key] = value.toString()
            }
        }

        val looksLikeCodexAlert = data["type"] == "codex_notification" || data.containsKey("toastId")
        if (!looksLikeCodexAlert) {
            return
        }

        val message = AlertStore.createMessageFromPayload(
            data = data,
            notificationTitle = data["title"],
            notificationBody = data["body"]
        )
        store.addMessage(message)
    }

    private fun refreshTokenUi() {
        val token = store.getFcmToken()
        val notificationStatus = if (NotificationHelper.canPostNotifications(this)) {
            "notifications allowed"
        } else {
            "notifications blocked"
        }
        val tokenStatus = when {
            token.isBlank() -> "token unavailable"
            store.tokenNeedsPcConfigUpdate() -> "token changed, update PC config"
            else -> "token ready"
        }
        statusView.text = "$notificationStatus - $tokenStatus"
        tokenView.text = if (token.isBlank()) {
            "Loading FCM token..."
        } else {
            token
        }
    }

    private fun copyToken() {
        val token = store.getFcmToken()
        if (token.isBlank()) {
            Toast.makeText(this, "Token is not ready yet.", Toast.LENGTH_SHORT).show()
            return
        }
        val clipboard = getSystemService(ClipboardManager::class.java)
        clipboard.setPrimaryClip(ClipData.newPlainText("Codex Alert FCM token", token))
        store.acknowledgeTokenCopied()
        refreshTokenUi()
        Toast.makeText(this, "Token copied.", Toast.LENGTH_SHORT).show()
    }

    private fun renderInbox() {
        inboxLayout.removeAllViews()
        val messages = store.getMessages()
        if (messages.isEmpty()) {
            inboxLayout.addView(TextView(this).apply {
                text = "No alerts yet."
                textSize = 14f
                setTextColor(Color.rgb(88, 92, 99))
                setPadding(0, dp(24), 0, 0)
            })
            return
        }

        messages.forEach { message ->
            inboxLayout.addView(messageView(message))
        }
    }

    private fun messageView(message: AlertMessage): View {
        val container = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(Color.WHITE)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        container.addView(TextView(this).apply {
            text = message.title.ifBlank { "Codex" }
            textSize = 16f
            setTextColor(Color.rgb(26, 28, 33))
            typeface = android.graphics.Typeface.DEFAULT_BOLD
        })
        container.addView(TextView(this).apply {
            text = message.body.ifBlank { "(empty notification body)" }
            textSize = 14f
            setTextColor(Color.rgb(44, 47, 53))
            setPadding(0, dp(4), 0, dp(6))
        })
        container.addView(TextView(this).apply {
            text = listOf(message.pcName, message.receivedAtUtc).filter { it.isNotBlank() }.joinToString(" - ")
            textSize = 12f
            setTextColor(Color.rgb(112, 116, 124))
        })

        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(0, 0, 0, dp(10))
            addView(container)
        }
    }

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()

    companion object {
        private const val REQUEST_NOTIFICATIONS = 1001
    }
}
