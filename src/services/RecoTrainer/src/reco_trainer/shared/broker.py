"""Ortak altyapı (shared_kernel): FastStream RabbitBroker (tek örnek). Handler'lar buna subscribe eder.

Binding'i TÜKETİCİ kurar (soğuk-açılış kayıp dersi, 007): Python kendi kuyruk+exchange bağını deklare eder.
"""

from __future__ import annotations

from faststream.rabbit import RabbitBroker

from reco_trainer.config import settings

broker = RabbitBroker(settings.rabbitmq_url)