from typing import Optional

from pydantic import BaseModel, Field
from .task_logic import Action


class TrialOutCome(BaseModel):
    """A model that represents the outcome of a trial in the experiment."""
    response_time: Optional[float] = Field(default=None, description="The time from response cue to hitting the threshold.")
    is_successful: bool = Field(default=False, description="Whether the trial was successful or not.")
    action: Action = Field(description="The action produced by the subject during the trial.")