import logging
import typing as t

import pandas as pd
from contraqctor import contract, qc
from contraqctor.contract.harp import HarpDevice

from aind_behavior_telekinesis.rig import AindBehaviorTelekinesisRig

logger = logging.getLogger(__name__)


class TelekinesisQcSuite(qc.Suite):
    def __init__(self, dataset: contract.Dataset):
        self.dataset = dataset

    def test_end_session_exists(self):
        """Check that the session has an end event."""
        end_session = self.dataset["Behavior"]["SoftwareEvents"]["EndSession"]

        if not end_session.has_data:
            return self.fail_test(
                None, "EndSession event does not exist. Session may be corrupted or not ended properly."
            )

        assert isinstance(end_session.data, pd.DataFrame)
        if end_session.data.empty:
            return self.fail_test(None, "No data in EndSession. Session may be corrupted or not ended properly.")
        else:
            return self.pass_test(None, "EndSession event exists with data.")


def make_qc_runner(dataset: contract.Dataset) -> qc.Runner:
    """
    Create a QC runner with checks specific to the telekinesis task.

    Args:
        dataset: The loaded dataset to run QC checks on.

    Returns:
        A configured QC runner with all registered checks.
    """
    _runner = qc.Runner()
    dataset.load_all(strict=False)
    exclude: list[contract.DataStream] = []
    rig: AindBehaviorTelekinesisRig = dataset["Behavior"]["Configuration"]["Rig"].data

    # Exclude commands to Harp boards as these are tested separately
    for cmd in dataset["Behavior"]["HarpCommands"]:
        for stream in cmd:
            if isinstance(stream, contract.harp.HarpRegister):
                exclude.append(stream)

    # Add the outcome of the dataset loading step to the automatic qc
    _runner.add_suite(qc.contract.ContractTestSuite(dataset.collect_errors(), exclude=exclude), group="Data contract")

    # Add Harp tests for ALL Harp devices in the dataset
    for stream in (_r := dataset["Behavior"]):
        if isinstance(stream, HarpDevice):
            commands = t.cast(HarpDevice, _r["HarpCommands"][stream.name])
            _runner.add_suite(qc.harp.HarpDeviceTestSuite(stream, commands), stream.name)

    # Add Harp Hub tests
    _runner.add_suite(
        qc.harp.HarpHubTestSuite(
            dataset["Behavior"]["HarpClockGenerator"],
            [harp_device for harp_device in dataset["Behavior"] if isinstance(harp_device, HarpDevice)],
        ),
        "HarpHub",
    )

    if rig.harp_environment_sensor is not None:
        _runner.add_suite(
            qc.harp.HarpEnvironmentSensorTestSuite(dataset["Behavior"]["HarpEnvironmentSensor"]),
            "HarpEnvironmentSensor",
        )

    _runner.add_suite(qc.harp.HarpLicketySplitTestSuite(dataset["Behavior"]["HarpLickometer"]), "HarpLickometer")

    # Add camera qc
    for camera in dataset["BehaviorVideos"]:
        _runner.add_suite(
            qc.camera.CameraTestSuite(camera, expected_fps=rig.triggered_camera_controller.frame_rate), camera.name
        )

    # Add Csv tests
    csv_streams = [stream for stream in dataset.iter_all() if isinstance(stream, contract.csv.Csv)]
    for stream in csv_streams:
        _runner.add_suite(qc.csv.CsvTestSuite(stream), stream.name)

    # Add the Telekinesis specific tests
    _runner.add_suite(TelekinesisQcSuite(dataset), "Telekinesis")
    return _runner
