import unittest
from select_input import select_input
class SelectionChecks(unittest.TestCase):
    def test_select_input_not_speaker(self):
        d=[dict(name='Steam Streaming Microphone',max_input_channels=0,hostapi=0,default_samplerate=1),dict(name='Steam Streaming Microphone',max_input_channels=2,hostapi=1,default_samplerate=48000)]
        self.assertEqual(select_input(d,[{'name':'MME'},{'name':'Windows WASAPI'}]),{'device':1,'rate':48000})
    def test_missing_fails_without_unrelated_microphone(self):
        with self.assertRaises(RuntimeError):select_input([dict(name='Realtek',max_input_channels=2)],[])
    def test_reenumerated_index(self):
        d=dict(name='Steam Streaming Microphone',max_input_channels=2,hostapi=0,default_samplerate=48000)
        self.assertEqual(select_input([dict(name='output',max_input_channels=0),d],[{'name':'Windows WASAPI'}])['device'],1)
