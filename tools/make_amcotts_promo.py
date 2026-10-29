import cv2
import numpy as np
from pathlib import Path

ROOT = Path(r"C:\Users\HP GB\Videos\Screen Recordings")
OUT = Path("promo-output")
OUT.mkdir(exist_ok=True)
W, H, FPS = 1920, 1080, 30
output = OUT / "amcotts-workshop-promo-premium.mp4"
writer = cv2.VideoWriter(str(output), cv2.VideoWriter_fourcc(*"mp4v"), FPS, (W, H))

NAVY = (46, 27, 11)
NAVY_LIGHT = (66, 43, 23)
YELLOW = (3, 183, 255)
WHITE = (245, 247, 250)
MUTED = (190, 207, 220)

def gradient_background():
    frame = np.zeros((H, W, 3), dtype=np.uint8)
    for y in range(H):
        blend = y / max(H - 1, 1)
        frame[y, :] = [int(NAVY[i] * (1 - blend) + NAVY_LIGHT[i] * blend) for i in range(3)]
    return frame

def centered_text(frame, text, y, scale, color=WHITE, thickness=2):
    size, _ = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, scale, thickness)
    x = (W - size[0]) // 2
    cv2.putText(frame, text, (x, y), cv2.FONT_HERSHEY_SIMPLEX, scale, color, thickness, cv2.LINE_AA)

def title_card(title, subtitle, seconds=2.2):
    frame = gradient_background()
    cv2.rectangle(frame, (112, 126), (282, 296), YELLOW, -1)
    cv2.putText(frame, "AM", (139, 242), cv2.FONT_HERSHEY_SIMPLEX, 2.15, NAVY, 4, cv2.LINE_AA)
    cv2.putText(frame, "AMCOTTS  /  WORKSHOP CONTROL CENTER", (112, 390), cv2.FONT_HERSHEY_SIMPLEX, .7, YELLOW, 2, cv2.LINE_AA)
    cv2.putText(frame, title, (112, 560), cv2.FONT_HERSHEY_SIMPLEX, 2.05, WHITE, 4, cv2.LINE_AA)
    cv2.putText(frame, subtitle, (116, 642), cv2.FONT_HERSHEY_SIMPLEX, .85, MUTED, 2, cv2.LINE_AA)
    cv2.line(frame, (112, 710), (760, 710), YELLOW, 4, cv2.LINE_AA)
    cv2.putText(frame, "LIVE WORKSHOP OPERATIONS", (112, 758), cv2.FONT_HERSHEY_SIMPLEX, .55, MUTED, 1, cv2.LINE_AA)
    for index in range(int(seconds * FPS)):
        fade = min(1.0, index / 18, (int(seconds * FPS) - index) / 18)
        writer.write((frame * max(.2, fade)).astype(np.uint8))

def lower_third(frame, kicker, title, subtitle, scene_number):
    kicker = " ".join("".join(ch for ch in kicker if ord(ch) < 128).split())
    overlay = frame.copy()
    y = H - 196
    cv2.rectangle(overlay, (54, y), (1180, H - 48), NAVY, -1)
    cv2.addWeighted(overlay, .88, frame, .12, 0, frame)
    cv2.rectangle(frame, (54, y), (68, H - 48), YELLOW, -1)
    cv2.putText(frame, f"{scene_number:02d}", (94, y + 58), cv2.FONT_HERSHEY_SIMPLEX, 1.15, YELLOW, 3, cv2.LINE_AA)
    cv2.putText(frame, kicker.upper(), (184, y + 44), cv2.FONT_HERSHEY_SIMPLEX, .48, YELLOW, 1, cv2.LINE_AA)
    cv2.putText(frame, title, (184, y + 102), cv2.FONT_HERSHEY_SIMPLEX, .9, WHITE, 2, cv2.LINE_AA)
    cv2.putText(frame, subtitle, (184, y + 146), cv2.FONT_HERSHEY_SIMPLEX, .52, MUTED, 1, cv2.LINE_AA)
    cv2.putText(frame, "AMCOTTS  /  LIVE", (W - 360, 62), cv2.FONT_HERSHEY_SIMPLEX, .45, YELLOW, 1, cv2.LINE_AA)

scene_counter = 0

def segment(filename, start, end, kicker, title, subtitle, scene_number=None):
    global scene_counter
    scene_counter += 1
    scene_number = scene_number or scene_counter
    cap = cv2.VideoCapture(str(ROOT / filename))
    cap.set(cv2.CAP_PROP_POS_MSEC, start * 1000)
    frames = int((end - start) * FPS)
    for _ in range(frames):
        ok, frame = cap.read()
        if not ok:
            break
        frame = cv2.resize(frame, (W, H), interpolation=cv2.INTER_AREA)
        lower_third(frame, kicker, title, subtitle, scene_number)
        frame = (frame * min(1.0, (_ + 1) / 15, (frames - _) / 15)).astype(np.uint8)
        writer.write(frame)
    cap.release()

title_card("WORKSHOP CONTROL CENTER", "From vehicle intake to confirmed repair action", 2.0)
segment("Screen Recording 2026-10-03 234511.mp4", 0, 13, "01 · SECURE ACCESS", "Every workshop starts with control", "Role-based sign-in for supervisors, mechanics and service clerks")
segment("Screen Recording 2026-10-03 234511.mp4", 45, 60, "02 · LIVE OVERVIEW", "See the workshop at a glance", "Real-time throughput, active jobs and operational alerts")
segment("Screen Recording 2026-10-03 234511.mp4", 70, 91, "03 · VEHICLE INTAKE", "Create a complete job card", "Capture registration, customer, truck model and reported symptoms")
segment("Screen Recording 2026-10-03 234809.mp4", 30, 42, "04 · WORKSHOP QUEUE", "Keep every job moving", "Track awaiting entry, in-workshop and quality-check stages")
segment("Screen Recording 2026-10-03 234809.mp4", 42, 63, "05 · AI-ASSISTED INSPECTION", "Turn symptoms into useful suggestions", "AI supports inspection; a mechanic confirms the actual fault")
segment("Screen Recording 2026-10-03 235103.mp4", 46, 69, "06 · MECHANIC ALLOCATION", "Assign the right technician", "Match skills, availability and workload before work begins")
title_card("MOVE EVERY REPAIR FORWARD", "Amcotts Workshop Control Center", 3.0)
writer.release()
print(output.resolve())
