# pylint: disable=locally-disabled,line-too-long,missing-docstring
import os
import argparse
import logging
import shutil

import msftkube as mk

mk.steps.acs.TEMPLATE_TRANSFORMERS = [
    # _transformer_encrypt_storages,
    # mk.steps.acs._transformer_do_not_use_port_22,
    # _transformer_add_security_extension,
    mk.steps.acs._parameters_change_to_keyvault_reference('servicePrincipalClientId'),
    mk.steps.acs._parameters_change_to_keyvault_reference('sshRSAPublicKey'),
    mk.steps.acs._parameters_change_to_keyvault_reference('servicePrincipalClientSecret')
]

def main():

    commands = mk.commands.DEFAULT_COMMANDS

    context = mk.context.create(commands.keys())

    # determine paths. Script could be in repository or a build drop
    script_dir = os.path.dirname(__file__)
    root_dir = os.path.abspath(os.path.join(script_dir, '..'))
    if os.path.isdir(os.path.join(root_dir, 'src')):
        # we are in repository
        drop_dir = os.path.join(root_dir, 'build')
        mk.util.ensure_dir(drop_dir)
        def copy_scripts_into_drop(spec, context): # pylint: disable=unused-argument
            mk.logger.info('task.py - Copying scripts into drop folder')
            scripts_dest = os.path.join(drop_dir, 'scripts')
            mk.util.copytree_clobber(script_dir, scripts_dest)
            mk.util.copytree_clobber(mk.__path__[0], os.path.join(scripts_dest, 'msftkube'))
            mk.util.copytree_clobber(os.path.join(root_dir, 'deploy'), os.path.join(drop_dir, 'deploy'))
        commands['publish'] += [copy_scripts_into_drop]
    else:
        # we are in a build drop outside of the repository
        drop_dir = root_dir

    mk.logger.info('task.py - Drop dir path is %s', drop_dir)

    # write log file to drop
    mk.logger.addFileHandler(os.path.join(drop_dir, 'build.log'))

    # Seed spec from paths and args
    seed_spec = {'paths': {'drop': drop_dir}}

    # provide user args & context to steps
    
    # read specs from files into spec
    spec = mk.spec.create(root_dir, [seed_spec, os.path.join(root_dir, 'deploy', 'specs')])

    # run any steps selected
    mk.commands.execute(commands, spec, context)

if __name__ == '__main__':
    main()
