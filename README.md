AUFIT
=====
AUFIT is the acronym for Atari Universal Floppy-Disk Image Tool.

The primary function of the Aufit program is to analyzes the content of Atari Floppy Disks. Several Graphical and Text views are provided to display the results from this analysis.

Another important function of the program is to convert from an image file format to a different format. Therefore, Aufit program is frequently used to convert stream files to a format supported by the different Atari emulators. For example it is possible to convert KryoFlux .raw stream files to the Pasti .stx files used by many Atari emulators.

Currently the supported input formats are .scp (SuperCard Pro) and .raw (KryoFlux) and the supported output formats are .stx format for protected diskette and .st, .msa for non-protected diskettes.

Beside the capability to convert, Aufit performs deep analysis of floppy disk images and displays the corresponding information. For example, it is possible to display the "layout" of a FD, to detect most of the key disk protections used by a game, or to analyze the quality of the disk image.

The deep analysis of the FD content allows in some cases an automatic correction of errors found during imaging. For example it is sometimes possible to correctly save an image with partially damaged sectors.

The program has been designed to be as simple as possible to use for casual users only interested in converting files. In fact, conversion of many images can be done in batch mode without user intervention. However, the program also provides powerful features to deeply analyze floppy content for more advanced users.


The program is developed in C# and the GUI uses Microsoft WPF Framework.

Currently GitHub contains a very limited preliminary version of the program limited to a library for reading KryoFlux Stream File.
Hopefully I will publish the complete version of the program soon :)
