import cv2
import json
import subprocess
from pathlib import Path

from ultralytics import YOLO


INPUT_PATH = Path("input.mp4")
TEMP_VIDEO_PATH = Path("annotated_video_temp.mp4")
OUTPUT_VIDEO_PATH = Path("output.mp4")
OUTPUT_JSON_PATH = Path("detections.json")

MODEL_PATH = "yolo26s.pt"

CONFIDENCE_THRESHOLD = 0.3

# COCO
TARGET_CLASSES = [
    0,  # person
    2,  # car
    3,  # motorcycle
    5,  # bus
    7,  # truck
]


def main():
    model = YOLO(MODEL_PATH)

    cap = cv2.VideoCapture(str(INPUT_PATH))

    if not cap.isOpened():
        raise RuntimeError(f"Failed to open video: {INPUT_PATH}")

    fps = cap.get(cv2.CAP_PROP_FPS)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    frame_count = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))

    writer = create_video_writer(
        TEMP_VIDEO_PATH,
        fps,
        width,
        height
    )

    frames = []

    try:
        process_video(
            model,
            cap,
            writer,
            fps,
            frames
        )
    finally:
        cap.release()
        writer.release()

    write_json(
        OUTPUT_JSON_PATH,
        fps,
        width,
        height,
        frame_count,
        frames
    )

    mux_audio(
        TEMP_VIDEO_PATH,
        INPUT_PATH,
        OUTPUT_VIDEO_PATH
    )

    TEMP_VIDEO_PATH.unlink(missing_ok=True)


def create_video_writer(path, fps, width, height):
    fourcc = cv2.VideoWriter_fourcc(*"mp4v")

    writer = cv2.VideoWriter(
        str(path),
        fourcc,
        fps,
        (width, height)
    )

    if not writer.isOpened():
        raise RuntimeError(f"Failed to create video: {path}")

    return writer


def process_video(model, cap, writer, fps, frames):
    frame_index = 0

    while True:
        success, frame = cap.read()

        if not success:
            break

        result = detect(model, frame)

        frames.append(
            create_frame_result(
                result,
                frame_index,
                fps
            )
        )

        annotated_frame = result.plot()

        writer.write(annotated_frame)

        frame_index += 1


def detect(model, frame):
    return model.predict(
        source=frame,
        conf=CONFIDENCE_THRESHOLD,
        classes=TARGET_CLASSES,
        verbose=False
    )[0]


def create_frame_result(result, frame_index, fps):
    return {
        "frame": frame_index,
        "timeSec": frame_index / fps,
        "detections": create_detections(result)
    }


def create_detections(result):
    detections = []

    for box in result.boxes:
        class_id = int(box.cls.item())
        confidence = float(box.conf.item())

        x1, y1, x2, y2 = box.xyxy[0].tolist()
        nx1, ny1, nx2, ny2 = box.xyxyn[0].tolist()

        detections.append({
            "classId": class_id,
            "className": result.names[class_id],
            "confidence": confidence,

            "boundingBox": {
                "x1": x1,
                "y1": y1,
                "x2": x2,
                "y2": y2
            },

            "normalizedBoundingBox": {
                "x1": nx1,
                "y1": ny1,
                "x2": nx2,
                "y2": ny2
            }
        })

    return detections


def write_json(
    path,
    fps,
    width,
    height,
    frame_count,
    frames
):
    data = {
        "video": {
            "file": INPUT_PATH.name,
            "width": width,
            "height": height,
            "fps": fps,
            "frameCount": frame_count
        },
        "frames": frames
    }

    with path.open("w", encoding="utf-8") as file:
        json.dump(
            data,
            file,
            ensure_ascii=False,
            indent=2
        )


def mux_audio(video_path, original_path, output_path):
    command = [
        "ffmpeg",
        "-y",

        "-i", str(video_path),
        "-i", str(original_path),

        "-map", "0:v:0",
        "-map", "1:a?",

        "-c:v", "copy",
        "-c:a", "aac",
        "-b:a", "192k",

        "-shortest",

        str(output_path)
    ]

    subprocess.run(
        command,
        check=True
    )


if __name__ == "__main__":
    main()