let audio: HTMLAudioElement | null = null
let unlocked = false
let looping = false
let pendingPlay = false
let unlockBound = false
let audioCtx: AudioContext | null = null
let oscillator: OscillatorNode | null = null
let gainNode: GainNode | null = null
let sirenTimer: number | null = null
let usingSynth = false

function alarmSrc() {
  const base = import.meta.env.BASE_URL || '/'
  return `${base.endsWith('/') ? base : `${base}/`}sos-alarm.wav`
}

function ensureAudio() {
  if (!audio) {
    audio = new Audio(alarmSrc())
    audio.preload = 'auto'
    audio.loop = true
  }
  return audio
}

function ensureAudioContext() {
  if (!audioCtx) {
    audioCtx = new AudioContext()
  }
  return audioCtx
}

function stopSynth() {
  if (sirenTimer != null) {
    window.clearInterval(sirenTimer)
    sirenTimer = null
  }
  try {
    oscillator?.stop()
  } catch {
    /* ignore */
  }
  oscillator?.disconnect()
  gainNode?.disconnect()
  oscillator = null
  gainNode = null
  usingSynth = false
}

function startSynthSiren() {
  stopSynth()
  const ctx = ensureAudioContext()
  const osc = ctx.createOscillator()
  const gain = ctx.createGain()
  osc.type = 'square'
  osc.frequency.value = 880
  gain.gain.value = 0.18
  osc.connect(gain)
  gain.connect(ctx.destination)
  osc.start()
  oscillator = osc
  gainNode = gain
  usingSynth = true
  let high = true
  sirenTimer = window.setInterval(() => {
    high = !high
    if (oscillator) {
      oscillator.frequency.setTargetAtTime(high ? 880 : 560, ctx.currentTime, 0.02)
    }
  }, 420)
}

async function resumeContext() {
  const ctx = ensureAudioContext()
  if (ctx.state === 'suspended') {
    await ctx.resume()
  }
}

export function unlockSosAudio() {
  void resumeContext().catch(() => {})
  if (unlocked) {
    if (pendingPlay) {
      pendingPlay = false
      playSosAlarm()
    }
    return
  }

  const clip = ensureAudio()
  clip.volume = 0
  void clip.play().then(() => {
    clip.pause()
    clip.currentTime = 0
    clip.volume = 1
    unlocked = true
    if (pendingPlay) {
      pendingPlay = false
      playSosAlarm()
    }
  }).catch(() => {
    unlocked = false
  })
}

export function bindSosAudioUnlock() {
  if (unlockBound) {
    return
  }
  unlockBound = true
  const unlock = () => {
    unlockSosAudio()
  }
  window.addEventListener('pointerdown', unlock)
  window.addEventListener('keydown', unlock)
  window.addEventListener('touchstart', unlock, { passive: true })
}

export function playSosAlarm() {
  looping = true
  pendingPlay = false
  const clip = ensureAudio()
  clip.loop = true
  clip.volume = 1
  clip.currentTime = 0

  void resumeContext()
    .then(() => clip.play())
    .then(() => {
      unlocked = true
      stopSynth()
    })
    .catch(() => {
      // Autoplay blocked or file missing — try synthesized siren, else wait for gesture.
      try {
        startSynthSiren()
        unlocked = true
      } catch {
        pendingPlay = true
        unlocked = false
        bindSosAudioUnlock()
      }
    })
}

export function stopSosAlarm() {
  looping = false
  pendingPlay = false
  stopSynth()
  if (!audio) {
    return
  }
  audio.pause()
  audio.currentTime = 0
}

export function isSosAlarmPlaying() {
  if (!looping) {
    return false
  }
  if (usingSynth) {
    return true
  }
  return !!audio && !audio.paused
}
