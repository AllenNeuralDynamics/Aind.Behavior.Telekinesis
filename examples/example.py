import datetime
import os

import aind_behavior_services.calibration.load_cells as lcc
import aind_behavior_services.rig as rig
from aind_behavior_services import db_utils as db
from aind_behavior_services.calibration.aind_manipulator import (
    AindManipulatorCalibration,
    AindManipulatorCalibrationInput,
    AindManipulatorCalibrationOutput,
    Axis,
    AxisConfiguration,
    ManipulatorPosition,
)
from aind_behavior_services.calibration.water_valve import (
    Measurement,
    WaterValveCalibration,
    WaterValveCalibrationInput,
    WaterValveCalibrationOutput,
)
from aind_behavior_services.session import AindBehaviorSessionModel
from aind_behavior_telekinesis.rig import AindManipulatorDevice, AindTelekinesisRig, RigCalibration
from aind_behavior_telekinesis.task_logic import (
    AindTelekinesisTaskLogic,
)


def mock_session() -> AindBehaviorSessionModel:
    """Generates a mock AindBehaviorSessionModel model"""
    return AindBehaviorSessionModel(
        date=datetime.datetime.now(tz=datetime.timezone.utc),
        experiment="Telekinesis",
        root_path="c://",
        subject="test",
        notes="test session",
        experiment_version="0.1.0",
        allow_dirty_repo=True,
        skip_hardware_validation=False,
        experimenter=["Foo", "Bar"],
    )


def mock_rig() -> AindTelekinesisRig:
    """Generates a mock AindVrForagingRig model"""

    manipulator_calibration = AindManipulatorCalibration(
        output=AindManipulatorCalibrationOutput(),
        input=AindManipulatorCalibrationInput(
            full_step_to_mm=(ManipulatorPosition(x=0.010, y1=0.010, y2=0.010, z=0.010)),
            axis_configuration=[
                AxisConfiguration(axis=Axis.Y1, min_limit=-1, max_limit=15000),
                AxisConfiguration(axis=Axis.Y2, min_limit=-1, max_limit=15000),
                AxisConfiguration(axis=Axis.X, min_limit=-1, max_limit=15000),
                AxisConfiguration(axis=Axis.Z, min_limit=-1, max_limit=15000),
            ],
            homing_order=[Axis.Y1, Axis.Y2, Axis.X, Axis.Z],
            initial_position=ManipulatorPosition(y1=0, y2=0, x=0, z=0),
        ),
    )

    water_valve_input = WaterValveCalibrationInput(
        measurements=[
            Measurement(valve_open_interval=1, valve_open_time=1, water_weight=[1, 1], repeat_count=200),
            Measurement(valve_open_interval=2, valve_open_time=2, water_weight=[2, 2], repeat_count=200),
        ]
    )
    water_valve_calibration = WaterValveCalibration(
        input=water_valve_input, output=water_valve_input.calibrate_output(), date=datetime.datetime.now()
    )
    water_valve_calibration.output = WaterValveCalibrationOutput(slope=1, offset=0)  # For testing purposes

    video_writer = rig.VideoWriterFfmpeg(
        frame_rate=120, container_extension="mp4", output_arguments="-c:v h264_nvenc -vsync 0 -2pass "
    )

    load_cells_calibration = lcc.LoadCellsCalibration(
        output=lcc.LoadCellsCalibrationOutput(),
        input=lcc.LoadCellsCalibrationInput(),
        date=datetime.datetime.now(),
    )
    return AindTelekinesisRig(
        rig_name="test_rig",
        triggered_camera_controller=rig.CameraController[rig.SpinnakerCamera](
            frame_rate=120,
            cameras={
                "FaceCamera": rig.SpinnakerCamera(
                    serial_number="SerialNumber", binning=1, exposure=5000, gain=0, video_writer=video_writer
                ),
                "SideCamera": rig.SpinnakerCamera(
                    serial_number="SerialNumber", binning=1, exposure=5000, gain=0, video_writer=video_writer
                ),
            },
        ),
        harp_load_cells=lcc.LoadCells(port_name="COM4", calibration=load_cells_calibration),
        monitoring_camera_controller=rig.CameraController[rig.WebCamera](cameras={"WebCam0": rig.WebCamera(index=0)}),
        harp_behavior=rig.HarpBehavior(port_name="COM3"),
        harp_lickometer=rig.HarpLickometer(port_name="COM5"),
        harp_clock_generator=rig.HarpClockGenerator(port_name="COM6"),
        harp_analog_input=None,
        manipulator=AindManipulatorDevice(port_name="COM9", calibration=manipulator_calibration),
        screen=rig.Screen(display_index=1),
        calibration=RigCalibration(water_valve=water_valve_calibration),
    )


def mock_task_logic() -> AindTelekinesisTaskLogic:
    raise NotImplementedError("This function is not implemented")


def mock_subject_database() -> db.SubjectDataBase:
    """Generates a mock database object"""
    database = db.SubjectDataBase()
    database.add_subject("test", db.SubjectEntry(task_logic_target="preward_intercept_stageA"))
    database.add_subject("test2", db.SubjectEntry(task_logic_target="does_notexist"))
    return database


def main(path_seed: str = "./local/{schema}.json"):
    example_session = mock_session()
    example_rig = mock_rig()
    example_task_logic = mock_task_logic()
    example_database = mock_subject_database()

    os.makedirs(os.path.dirname(path_seed), exist_ok=True)

    models = [example_task_logic, example_session, example_rig, example_database]

    for model in models:
        with open(path_seed.format(schema=model.__class__.__name__), "w", encoding="utf-8") as f:
            f.write(model.model_dump_json(indent=2))


if __name__ == "__main__":
    main()
