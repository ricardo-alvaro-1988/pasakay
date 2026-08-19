import { useEffect, useState } from 'react'
import logo from './logo-circle.png'
import motorcycleHero from './assets/motorcycle.png'
import tricycleHero from './assets/tricycle.png'
import { VEHICLE_ART } from './vehicle-art'

const LOGIN_COPY_LOOP_MS = 10000

type LoginBrandPanelProps = {
  kicker: string
  title: string
  description: string
  showPoints?: boolean
  animateCopy?: boolean
}

function AnimatedWords({
  text,
  cycle,
  className,
  tag: Tag = 'span',
  inStart = 0,
  inStep = 0.16,
  outStart = 6.6,
  outStep = 0.1,
}: {
  text: string
  cycle: number
  className?: string
  tag?: 'span' | 'h1' | 'p'
  inStart?: number
  inStep?: number
  outStart?: number
  outStep?: number
}) {
  return (
    <Tag className={className}>
      {text.split(/\s+/).map((word, index) => (
        <span
          key={`${cycle}-${word}-${index}`}
          className="login-word"
          style={{
            animationDelay: `${inStart + index * inStep}s, ${outStart + index * outStep}s`,
          }}
        >
          {word}
        </span>
      ))}
    </Tag>
  )
}

export function LoginBrandPanel({
  kicker,
  title,
  description,
  showPoints = false,
  animateCopy = false,
}: LoginBrandPanelProps) {
  const [cycle, setCycle] = useState(0)

  useEffect(() => {
    if (!animateCopy) return
    const id = window.setInterval(() => setCycle((value) => value + 1), LOGIN_COPY_LOOP_MS)
    return () => window.clearInterval(id)
  }, [animateCopy])

  return (
    <section className="login-brand">
      <div className="login-brand-glow" aria-hidden="true" />
      <div className="login-brand-route" aria-hidden="true" />
      <div className="login-vehicles-scene" aria-hidden="true">
        <img className="login-vehicle login-vehicle-moto" src={motorcycleHero} alt="" />
        <img className="login-vehicle login-vehicle-trike" src={tricycleHero} alt="" />
      </div>
      <div className="login-brand-top">
        <img src={logo} alt="Ya! Pasakay" />
        <span>Ya! Pasakay</span>
      </div>
      <div key={cycle} className={`login-brand-copy${animateCopy ? ' login-brand-copy-alive' : ''}`}>
        {animateCopy ? (
          <>
            <AnimatedWords
              tag="p"
              className="login-kicker"
              cycle={cycle}
              text={kicker}
              inStart={0.05}
              inStep={0.16}
              outStart={6.4}
              outStep={0.12}
            />
            <AnimatedWords
              tag="h1"
              cycle={cycle}
              text={title}
              inStart={0.85}
              inStep={0.2}
              outStart={7.15}
              outStep={0.14}
            />
            <AnimatedWords
              tag="p"
              cycle={cycle}
              text={description}
              inStart={2.1}
              inStep={0.12}
              outStart={8.05}
              outStep={0.08}
            />
          </>
        ) : (
          <>
            <p className="login-kicker">{kicker}</p>
            <h1>{title}</h1>
            <p>{description}</p>
          </>
        )}
        {showPoints ? (
          <ul className="login-points">
            <li>Fast booking nearby</li>
            <li>Live rider tracking</li>
            <li>Cash, GCash, and Maya</li>
          </ul>
        ) : null}
      </div>
    </section>
  )
}

export function LoginTrustBar() {
  return (
    <ul className="login-trust" aria-label="Payments and live tracking">
      <li>
        <span className="login-trust-ico" aria-hidden="true">₱</span>
        <strong>Cash</strong>
      </li>
      <li>
        <span className="login-trust-ico login-trust-gcash" aria-hidden="true">G</span>
        <strong>GCash</strong>
      </li>
      <li>
        <span className="login-trust-ico login-trust-maya" aria-hidden="true">M</span>
        <strong>Maya</strong>
      </li>
      <li>
        <span className="login-trust-ico login-trust-live" aria-hidden="true">
          <span />
        </span>
        <strong>Live Tracking</strong>
      </li>
    </ul>
  )
}

export function LoginVehicleCards() {
  return (
    <div className="login-vehicle-cards" aria-hidden="true">
      <div className="login-vehicle-card">
        <img src={VEHICLE_ART.Motorcycle} alt="" />
        <div>
          <strong>Motorcycle</strong>
          <span>Quick rides · 1 passenger</span>
        </div>
      </div>
      <div className="login-vehicle-card">
        <img src={VEHICLE_ART.Tricycle} alt="" />
        <div>
          <strong>Tricycle</strong>
          <span>Local routes · more space</span>
        </div>
      </div>
    </div>
  )
}
