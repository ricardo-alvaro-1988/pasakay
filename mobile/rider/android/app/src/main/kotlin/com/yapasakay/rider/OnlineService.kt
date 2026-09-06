package com.yapasakay.rider

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.media.AudioAttributes
import android.media.MediaPlayer
import android.net.Uri
import android.os.Build
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.os.PowerManager
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat

class OnlineService : Service() {
    companion object {
        const val ACTION_START = "start"
        const val ACTION_RING = "ring"
        const val ACTION_STOP_RING = "stop_ring"
        const val ACTION_CHAT = "chat"
        private const val ONLINE_CHANNEL = "yp_online"
        /** Bumped so soft vibration settings apply on devices that already had older channels. */
        private const val OFFER_CHANNEL = "yp_job_offers_v4"
        private const val CHAT_CHANNEL = "yp_chat"
        /** Soft double-tap; no long continuous buzz. */
        private val OFFER_VIBE_TIMINGS = longArrayOf(0, 90, 70, 120)
        private val OFFER_VIBE_AMPS = intArrayOf(0, 110, 0, 150)
        private const val OFFER_PULSE_MS = 2800L
        private const val ONLINE_ID = 1001
        private const val OFFER_ID = 1002
        private const val CHAT_ID = 1003

        @Volatile
        var running = false
            private set

        fun start(context: Context): Boolean {
            return startCommand(context, Intent(context, OnlineService::class.java).setAction(ACTION_START))
        }

        fun stop(context: Context) {
            try {
                context.stopService(Intent(context, OnlineService::class.java))
            } catch (_: Throwable) {
            }
        }

        fun ring(context: Context, title: String, body: String): Boolean {
            return startCommand(
                context,
                Intent(context, OnlineService::class.java)
                    .setAction(ACTION_RING)
                    .putExtra("title", title)
                    .putExtra("body", body),
            )
        }

        fun stopRing(context: Context) {
            if (!running) return
            try {
                context.startService(Intent(context, OnlineService::class.java).setAction(ACTION_STOP_RING))
            } catch (_: Throwable) {
            }
        }

        fun pingChat(context: Context, title: String, body: String): Boolean {
            val app = context.applicationContext
            ensureChannels(app)
            return try {
                val nm = app.getSystemService(NotificationManager::class.java)
                val launch = Intent(app, MainActivity::class.java)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP)
                val flags = PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
                val open = PendingIntent.getActivity(app, 3, launch, flags)
                val notification = NotificationCompat.Builder(app, CHAT_CHANNEL)
                    .setSmallIcon(R.drawable.ic_stat_notify)
                    .setContentTitle(title)
                    .setContentText(body)
                    .setStyle(NotificationCompat.BigTextStyle().bigText(body))
                    .setPriority(NotificationCompat.PRIORITY_HIGH)
                    .setCategory(NotificationCompat.CATEGORY_MESSAGE)
                    .setAutoCancel(true)
                    .setContentIntent(open)
                    .setDefaults(Notification.DEFAULT_SOUND or Notification.DEFAULT_VIBRATE)
                    .build()
                nm.notify(CHAT_ID, notification)
                true
            } catch (_: Throwable) {
                startCommand(
                    context,
                    Intent(context, OnlineService::class.java)
                        .setAction(ACTION_CHAT)
                        .putExtra("title", title)
                        .putExtra("body", body),
                )
            }
        }

        fun ensureChannels(context: Context) {
            if (Build.VERSION.SDK_INT < 26) return
            try {
                val nm = context.getSystemService(NotificationManager::class.java)
                nm.createNotificationChannel(
                    NotificationChannel(ONLINE_CHANNEL, "Online status", NotificationManager.IMPORTANCE_LOW).apply {
                        description = "Shown while you are online waiting for jobs."
                        setSound(null, null)
                    },
                )
                val sound = Uri.parse("android.resource://${context.packageName}/${R.raw.offer_alarm}")
                val attrs = AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_ALARM)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                    .build()
                nm.createNotificationChannel(
                    NotificationChannel(OFFER_CHANNEL, "Job offers", NotificationManager.IMPORTANCE_HIGH).apply {
                        description = "Alerts when a new booking is waiting."
                        setSound(sound, attrs)
                        // Vibration is driven by softPulse() so it stays smooth (not a continuous rattle).
                        enableVibration(false)
                        lockscreenVisibility = Notification.VISIBILITY_PUBLIC
                    },
                )
                nm.createNotificationChannel(
                    NotificationChannel(CHAT_CHANNEL, "Trip chat", NotificationManager.IMPORTANCE_HIGH).apply {
                        description = "Pings when a customer sends a chat message."
                        enableVibration(true)
                        vibrationPattern = longArrayOf(0, 250, 120, 250)
                        lockscreenVisibility = Notification.VISIBILITY_PUBLIC
                    },
                )
            } catch (_: Throwable) {
            }
        }

        private fun startCommand(context: Context, intent: Intent): Boolean {
            return try {
                ContextCompat.startForegroundService(context, intent)
                true
            } catch (_: Throwable) {
                try {
                    context.startService(intent)
                    true
                } catch (_: Throwable) {
                    false
                }
            }
        }
    }

    private var player: MediaPlayer? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var ringing = false
    private val pulseHandler = Handler(Looper.getMainLooper())
    private val pulseRunnable = object : Runnable {
        override fun run() {
            if (!ringing) return
            softPulse()
            pulseHandler.postDelayed(this, OFFER_PULSE_MS)
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        running = true
        ensureChannels()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        try {
            promoteForeground()
            when (intent?.action) {
                ACTION_RING -> startRing(
                    intent.getStringExtra("title") ?: "New job offer",
                    intent.getStringExtra("body") ?: "Open Ya! Pasakay to accept.",
                )
                ACTION_CHAT -> showChat(
                    intent.getStringExtra("title") ?: "New chat",
                    intent.getStringExtra("body") ?: "Open Ya! Pasakay to reply.",
                )
                ACTION_STOP_RING -> stopRingInternal()
            }
        } catch (_: Throwable) {
            try {
                stopSelf()
            } catch (_: Throwable) {
            }
        }
        return START_STICKY
    }

    override fun onDestroy() {
        running = false
        stopRingInternal()
        super.onDestroy()
    }

    private fun promoteForeground() {
        val notification = onlineNotification()
        if (Build.VERSION.SDK_INT >= 34) {
            val locationOk = hasLocationPermission()
            val type = if (locationOk) {
                ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION or ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC
            } else {
                ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC
            }
            try {
                startForeground(ONLINE_ID, notification, type)
                return
            } catch (_: Throwable) {
            }
            try {
                startForeground(ONLINE_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)
                return
            } catch (_: Throwable) {
            }
        }
        try {
            startForeground(ONLINE_ID, notification)
        } catch (_: Throwable) {
        }
    }

    private fun hasLocationPermission(): Boolean {
        val fine = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED
        val coarse = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED
        return fine || coarse
    }

    private fun ensureChannels() {
        ensureChannels(this)
    }

    private fun openAppIntent(): PendingIntent {
        val launch = Intent(this, MainActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        val flags = PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        return PendingIntent.getActivity(this, 1, launch, flags)
    }

    private fun onlineNotification(): Notification {
        return NotificationCompat.Builder(this, ONLINE_CHANNEL)
            .setSmallIcon(R.drawable.ic_stat_notify)
            .setContentTitle("Ya! Pasakay")
            .setContentText("You are online. Waiting for jobs.")
            .setOngoing(true)
            .setContentIntent(openAppIntent())
            .setSilent(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }

    private fun startRing(title: String, body: String) {
        try {
            acquireWake()
            playAlarm()
            if (!ringing) {
                ringing = true
                softPulse()
                pulseHandler.removeCallbacks(pulseRunnable)
                pulseHandler.postDelayed(pulseRunnable, OFFER_PULSE_MS)
            }
            val open = openAppIntent()
            val notification = NotificationCompat.Builder(this, OFFER_CHANNEL)
                .setSmallIcon(R.drawable.ic_stat_notify)
                .setContentTitle(title)
                .setContentText(body)
                .setStyle(NotificationCompat.BigTextStyle().bigText(body))
                .setPriority(NotificationCompat.PRIORITY_MAX)
                .setCategory(NotificationCompat.CATEGORY_CALL)
                .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
                .setOngoing(true)
                .setAutoCancel(false)
                .setFullScreenIntent(open, true)
                .setContentIntent(open)
                .build()
            getSystemService(NotificationManager::class.java).notify(OFFER_ID, notification)
        } catch (_: Throwable) {
        }
    }

    private fun showChat(title: String, body: String) {
        try {
            val notification = NotificationCompat.Builder(this, CHAT_CHANNEL)
                .setSmallIcon(R.drawable.ic_stat_notify)
                .setContentTitle(title)
                .setContentText(body)
                .setStyle(NotificationCompat.BigTextStyle().bigText(body))
                .setPriority(NotificationCompat.PRIORITY_HIGH)
                .setCategory(NotificationCompat.CATEGORY_MESSAGE)
                .setAutoCancel(true)
                .setContentIntent(openAppIntent())
                .setDefaults(Notification.DEFAULT_SOUND or Notification.DEFAULT_VIBRATE)
                .build()
            getSystemService(NotificationManager::class.java).notify(CHAT_ID, notification)
        } catch (_: Throwable) {
        }
    }

    private fun stopRingInternal() {
        ringing = false
        pulseHandler.removeCallbacks(pulseRunnable)
        try {
            player?.stop()
        } catch (_: Throwable) {
        }
        try {
            player?.release()
        } catch (_: Throwable) {
        }
        player = null
        releaseWake()
        try {
            getSystemService(NotificationManager::class.java).cancel(OFFER_ID)
        } catch (_: Throwable) {
        }
        try {
            vibrator()?.cancel()
        } catch (_: Throwable) {
        }
    }

    private fun playAlarm() {
        try {
            if (player?.isPlaying == true) return
        } catch (_: Throwable) {
        }
        try {
            player?.release()
        } catch (_: Throwable) {
        }
        player = null
        try {
            val next = MediaPlayer()
            next.setAudioAttributes(
                AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_ALARM)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                    .build(),
            )
            next.setDataSource(this, Uri.parse("android.resource://$packageName/${R.raw.offer_alarm}"))
            next.isLooping = true
            next.setVolume(0.9f, 0.9f)
            next.prepare()
            next.start()
            player = next
        } catch (_: Throwable) {
            try {
                player?.release()
            } catch (_: Throwable) {
            }
            player = null
        }
    }

    /** Short soft double-tap once — never an infinite rattle loop. */
    private fun softPulse() {
        try {
            val vibe = vibrator() ?: return
            if (Build.VERSION.SDK_INT >= 26) {
                vibe.vibrate(
                    VibrationEffect.createWaveform(OFFER_VIBE_TIMINGS, OFFER_VIBE_AMPS, -1),
                )
            } else {
                @Suppress("DEPRECATION")
                vibe.vibrate(OFFER_VIBE_TIMINGS, -1)
            }
        } catch (_: Throwable) {
        }
    }

    private fun vibrator(): Vibrator? {
        return try {
            if (Build.VERSION.SDK_INT >= 31) {
                getSystemService(VibratorManager::class.java).defaultVibrator
            } else {
                @Suppress("DEPRECATION")
                getSystemService(VIBRATOR_SERVICE) as Vibrator
            }
        } catch (_: Throwable) {
            null
        }
    }

    private fun acquireWake() {
        try {
            if (wakeLock?.isHeld == true) return
            val pm = getSystemService(POWER_SERVICE) as PowerManager
            wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "yapasakay:offer").apply {
                setReferenceCounted(false)
                acquire(2 * 60 * 1000L)
            }
        } catch (_: Throwable) {
        }
    }

    private fun releaseWake() {
        try {
            if (wakeLock?.isHeld == true) wakeLock?.release()
        } catch (_: Throwable) {
        }
        wakeLock = null
    }
}
