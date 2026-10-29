import cv2
from pathlib import Path

source = Path(r"C:\Users\HP GB\Videos\Screen Recordings")
output = Path("tmp-video-contact")
output.mkdir(exist_ok=True)
files = sorted(source.glob("Screen Recording 2026-10-03*.mp4"))
for path in files:
    cap = cv2.VideoCapture(str(path))
    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    fps = cap.get(cv2.CAP_PROP_FPS) or 30
    frames = []
    for index in range(8):
        cap.set(cv2.CAP_PROP_POS_FRAMES, int(total * index / 7))
        ok, frame = cap.read()
        if not ok:
            continue
        frame = cv2.resize(frame, (480, 270))
        cv2.putText(frame, f"{int(index * total / 7 / fps)}s", (12, 252), cv2.FONT_HERSHEY_SIMPLEX, .7, (0, 190, 255), 2, cv2.LINE_AA)
        frames.append(frame)
    cap.release()
    if not frames:
        continue
    while len(frames) < 8:
        frames.append(frames[-1].copy())
    sheet = cv2.vconcat([cv2.hconcat(frames[:4]), cv2.hconcat(frames[4:8])])
    cv2.imwrite(str(output / f"{path.stem}.jpg"), sheet)
